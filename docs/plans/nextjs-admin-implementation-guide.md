# Next.js 14 Admin Panel - Implementation Guide

**Project:** Snakk.Admin (Next.js)
**Status:** Implementation Guide
**Created:** 2026-02-06
**Framework:** Next.js 14 with App Router

---

## Table of Contents

1. [Core Architecture Principles](#core-architecture-principles)
2. [Server vs Client Components](#server-vs-client-components)
3. [Phase 1: Foundation & Authentication](#phase-1-foundation--authentication)
4. [Phase 2: Dashboard Layout](#phase-2-dashboard-layout)
5. [Phase 3: User Management](#phase-3-user-management)
6. [Data Fetching Strategy](#data-fetching-strategy)
7. [Decision Tree Reference](#decision-tree-reference)

---

## Core Architecture Principles

### 1. Server Components by Default
- All components are Server Components unless explicitly marked with `'use client'`
- Server Components render on the server only
- Zero JavaScript sent to client for Server Components
- Can directly fetch data, access file system, etc.

### 2. Data Fetching at Page Level
- Server Components (pages) fetch initial data
- Pass data down to Client Components via props
- Client Components handle interactivity and subsequent fetches

### 3. Push Interactivity Down the Tree
- Keep `'use client'` boundary as low as possible
- Only mark components as Client when absolutely necessary
- Compose Server and Client Components strategically

### 4. Streaming and Suspense
- Use React Suspense for progressive loading
- Stream data as it becomes available
- Improve perceived performance

### 5. Parallel Data Fetching
- Use `Promise.all()` for independent queries
- Fetch multiple data sources simultaneously
- Reduce total loading time

---

## Server vs Client Components

### Server Components (Default)

**When to Use:**
- Static content display
- Initial data fetching
- Layouts and navigation (non-interactive)
- SEO-critical pages
- Large dependencies (charts libraries, etc.)

**Benefits:**
- Direct server resource access
- Zero client-side JavaScript
- Better performance
- Improved SEO
- Can use `async/await` directly

**Limitations:**
- Cannot use React hooks (`useState`, `useEffect`, etc.)
- Cannot use browser APIs
- Cannot handle user interactions
- Cannot use Context

**Example:**
```typescript
// app/(dashboard)/page.tsx
// Server Component - no 'use client' needed
export default async function DashboardPage() {
  // Direct data fetching
  const stats = await getStats();

  return (
    <div>
      <h1>Dashboard</h1>
      <StatsCards stats={stats} />
    </div>
  );
}
```

### Client Components ('use client')

**When to Use:**
- User interactions (clicks, inputs, etc.)
- React hooks (state, effects, etc.)
- Browser APIs (`window`, `localStorage`, etc.)
- Real-time features (SignalR, WebSockets)
- Context providers/consumers
- Third-party libraries that require client

**Benefits:**
- Full React feature set
- Browser API access
- Interactive UI
- Client-side state management

**Limitations:**
- Adds JavaScript to bundle
- Cannot directly access server resources
- Runs in browser (security implications)
- Cannot use `async` components

**Example:**
```typescript
'use client'

import { useState } from 'react';

export function UserTable({ initialData }) {
  const [search, setSearch] = useState('');

  return (
    <div>
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      {/* ... */}
    </div>
  );
}
```

---

## Phase 1: Foundation & Authentication

### Project Structure

```
snakk-admin/
├── src/
│   ├── app/
│   │   ├── (auth)/              # Auth route group
│   │   │   ├── login/
│   │   │   │   └── page.tsx     # Server Component
│   │   │   └── layout.tsx       # Minimal layout (Server)
│   │   ├── (dashboard)/         # Protected route group
│   │   │   ├── layout.tsx       # Main layout (Server)
│   │   │   ├── page.tsx         # Dashboard (Server)
│   │   │   ├── users/
│   │   │   ├── content/
│   │   │   ├── moderation/
│   │   │   ├── analytics/
│   │   │   └── settings/
│   │   ├── api/                 # API routes (optional)
│   │   ├── layout.tsx           # Root layout
│   │   └── globals.css
│   ├── components/
│   │   ├── providers/           # Client Components (providers)
│   │   ├── ui/                  # Shadcn components (Client)
│   │   ├── server/              # Reusable Server Components
│   │   └── client/              # Reusable Client Components
│   ├── lib/
│   │   ├── api/                 # API client functions
│   │   ├── auth/                # Auth utilities
│   │   │   ├── server.ts        # Server-side auth
│   │   │   └── client.ts        # Client-side auth
│   │   ├── utils.ts
│   │   └── constants.ts
│   ├── stores/                  # Zustand stores (Client)
│   ├── hooks/                   # React hooks (Client only)
│   └── types/
```

### Authentication Architecture

**Three-Layer Approach:**

1. **Middleware (Edge)** - Route protection
2. **Server Components** - Session verification
3. **Client Components** - Login forms, auth UI

#### Middleware (Edge Runtime)

**File:** `src/middleware.ts`

```typescript
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const token = request.cookies.get('admin_token');
  const isAuthPage = request.nextUrl.pathname.startsWith('/login');
  const isProtectedPage = request.nextUrl.pathname.startsWith('/dashboard') ||
                          request.nextUrl.pathname === '/';

  // Redirect to login if no token and accessing protected route
  if (!token && isProtectedPage) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  // Redirect to dashboard if logged in and accessing auth pages
  if (token && isAuthPage) {
    return NextResponse.redirect(new URL('/', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
};
```

#### Server-Side Auth Utilities

**File:** `src/lib/auth/server.ts`

```typescript
import { cookies } from 'next/headers';
import { jwtVerify } from 'jose';

export async function getServerSession() {
  const cookieStore = await cookies();
  const token = cookieStore.get('admin_token');

  if (!token) {
    return null;
  }

  try {
    const secret = new TextEncoder().encode(process.env.JWT_SECRET);
    const { payload } = await jwtVerify(token.value, secret);

    return {
      userId: payload.sub as string,
      email: payload.email as string,
      role: payload.role as string,
    };
  } catch {
    return null;
  }
}

export async function requireAuth() {
  const session = await getServerSession();

  if (!session) {
    throw new Error('Unauthorized');
  }

  return session;
}
```

#### Client-Side Auth

**File:** `src/lib/auth/client.ts`

```typescript
'use client'

import { create } from 'zustand';

interface AuthState {
  token: string | null;
  user: User | null;
  setAuth: (token: string, user: User) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  token: null,
  user: null,
  setAuth: (token, user) => set({ token, user }),
  clearAuth: () => set({ token: null, user: null }),
}));

export async function login(email: string, password: string) {
  const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
    credentials: 'include', // Important for cookies
  });

  if (!response.ok) {
    throw new Error('Login failed');
  }

  const data = await response.json();
  return data;
}

export async function logout() {
  await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/auth/logout`, {
    method: 'POST',
    credentials: 'include',
  });

  useAuthStore.getState().clearAuth();
}
```

#### Login Page (Server Component)

**File:** `src/app/(auth)/login/page.tsx`

```typescript
import { LoginForm } from '@/components/client/auth/LoginForm';
import { redirect } from 'next/navigation';
import { getServerSession } from '@/lib/auth/server';

export default async function LoginPage() {
  const session = await getServerSession();

  // Redirect if already logged in
  if (session) {
    redirect('/');
  }

  return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="w-full max-w-md">
        <h1 className="text-3xl font-bold mb-6">Admin Login</h1>
        <LoginForm />
      </div>
    </div>
  );
}
```

#### Login Form (Client Component)

**File:** `src/components/client/auth/LoginForm.tsx`

```typescript
'use client'

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { login } from '@/lib/auth/client';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/use-toast';

export function LoginForm() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const router = useRouter();
  const { toast } = useToast();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await login(email, password);
      router.push('/');
      router.refresh(); // Refresh server components
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Login failed',
        description: 'Invalid email or password',
      });
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Input
        type="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        placeholder="Email"
        required
      />
      <Input
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder="Password"
        required
      />
      <Button type="submit" className="w-full" disabled={isLoading}>
        {isLoading ? 'Logging in...' : 'Login'}
      </Button>
    </form>
  );
}
```

### Root Layout with Providers

**File:** `src/app/layout.tsx` (Server Component)

```typescript
import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';
import { Providers } from '@/components/providers/Providers';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Snakk Admin',
  description: 'Admin panel for Snakk forum platform',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={inter.className}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
```

**File:** `src/components/providers/Providers.tsx` (Client Component)

```typescript
'use client'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ThemeProvider } from '@/components/providers/ThemeProvider';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Toaster } from '@/components/ui/toaster';
import { useState } from 'react';

export function Providers({ children }: { children: React.ReactNode }) {
  // Create query client in component to ensure fresh instance per request
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60 * 1000, // 1 minute
            retry: 1,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider
        attribute="class"
        defaultTheme="system"
        enableSystem
        disableTransitionOnChange
      >
        <TooltipProvider>
          {children}
          <Toaster />
        </TooltipProvider>
      </ThemeProvider>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
```

---

## Phase 2: Dashboard Layout

### Protected Dashboard Layout

**File:** `src/app/(dashboard)/layout.tsx` (Server Component)

```typescript
import { requireAuth } from '@/lib/auth/server';
import { Sidebar } from '@/components/server/layout/Sidebar';
import { Navbar } from '@/components/client/layout/Navbar';
import { SignalRProvider } from '@/components/providers/SignalRProvider';

export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // This runs on server - if not authenticated, middleware redirects
  const session = await requireAuth();

  return (
    <SignalRProvider>
      <div className="flex h-screen overflow-hidden">
        {/* Server Component - static sidebar */}
        <Sidebar />

        <div className="flex-1 flex flex-col overflow-hidden">
          {/* Client Component - interactive navbar */}
          <Navbar user={session} />

          {/* Main content area */}
          <main className="flex-1 overflow-y-auto bg-gray-50 p-6">
            {children}
          </main>
        </div>
      </div>
    </SignalRProvider>
  );
}
```

### Sidebar (Server Component)

**File:** `src/components/server/layout/Sidebar.tsx`

```typescript
import Link from 'next/link';
import {
  LayoutDashboard,
  Users,
  FileText,
  Shield,
  Settings
} from 'lucide-react';

const navigation = [
  { name: 'Dashboard', href: '/', icon: LayoutDashboard },
  { name: 'Users', href: '/users', icon: Users },
  { name: 'Content', href: '/content', icon: FileText },
  { name: 'Moderation', href: '/moderation', icon: Shield },
  { name: 'Settings', href: '/settings', icon: Settings },
];

export function Sidebar() {
  return (
    <aside className="w-64 bg-gray-900 text-white">
      <div className="p-6">
        <h1 className="text-2xl font-bold">Snakk Admin</h1>
      </div>

      <nav className="space-y-1 px-3">
        {navigation.map((item) => (
          <Link
            key={item.name}
            href={item.href}
            className="flex items-center px-3 py-2 rounded-md hover:bg-gray-800 transition-colors"
          >
            <item.icon className="w-5 h-5 mr-3" />
            {item.name}
          </Link>
        ))}
      </nav>
    </aside>
  );
}
```

### Navbar (Client Component)

**File:** `src/components/client/layout/Navbar.tsx`

```typescript
'use client'

import { Bell, User } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { logout } from '@/lib/auth/client';
import { useRouter } from 'next/navigation';
import { useNotifications } from '@/hooks/useNotifications';

interface NavbarProps {
  user: {
    email: string;
    role: string;
  };
}

export function Navbar({ user }: NavbarProps) {
  const router = useRouter();
  const { notifications, unreadCount } = useNotifications();

  const handleLogout = async () => {
    await logout();
    router.push('/login');
    router.refresh();
  };

  return (
    <header className="bg-white border-b px-6 py-4 flex items-center justify-between">
      <div>
        {/* Breadcrumbs or page title could go here */}
      </div>

      <div className="flex items-center gap-4">
        {/* Notifications */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="relative">
              <Bell className="w-5 h-5" />
              {unreadCount > 0 && (
                <span className="absolute top-0 right-0 w-2 h-2 bg-red-500 rounded-full" />
              )}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-80">
            {/* Notification items */}
          </DropdownMenuContent>
        </DropdownMenu>

        {/* User menu */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon">
              <User className="w-5 h-5" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <div className="px-2 py-1.5 text-sm">
              <p className="font-medium">{user.email}</p>
              <p className="text-xs text-gray-500">{user.role}</p>
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleLogout}>
              Logout
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
```

### Dashboard Page with Streaming

**File:** `src/app/(dashboard)/page.tsx` (Server Component)

```typescript
import { Suspense } from 'react';
import { StatsCards } from '@/components/server/dashboard/StatsCards';
import { ActivityFeed } from '@/components/client/dashboard/ActivityFeed';
import { StatsCharts } from '@/components/client/dashboard/StatsCharts';
import { StatsCardsSkeleton } from '@/components/server/dashboard/StatsCardsSkeleton';
import { getStats, getRecentActivity } from '@/lib/api/dashboard';

export default async function DashboardPage() {
  // These fetch in parallel on the server
  const [stats, recentActivity] = await Promise.all([
    getStats(),
    getRecentActivity(),
  ]);

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold">Dashboard</h1>

      {/* Server Component - stats cards */}
      <Suspense fallback={<StatsCardsSkeleton />}>
        <StatsCards stats={stats} />
      </Suspense>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Client Component - interactive charts */}
        <StatsCharts initialData={stats.chartData} />

        {/* Client Component - real-time activity feed */}
        <ActivityFeed initialActivity={recentActivity} />
      </div>
    </div>
  );
}
```

### Stats Cards (Server Component)

**File:** `src/components/server/dashboard/StatsCards.tsx`

```typescript
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Users, MessageSquare, Flag, TrendingUp } from 'lucide-react';

interface Stats {
  totalUsers: number;
  activeUsers: number;
  totalDiscussions: number;
  pendingReports: number;
  userGrowth: number;
}

interface StatsCardsProps {
  stats: Stats;
}

export function StatsCards({ stats }: StatsCardsProps) {
  const cards = [
    {
      title: 'Total Users',
      value: stats.totalUsers.toLocaleString(),
      icon: Users,
      trend: `+${stats.userGrowth}%`,
    },
    {
      title: 'Active Users (24h)',
      value: stats.activeUsers.toLocaleString(),
      icon: TrendingUp,
    },
    {
      title: 'Discussions',
      value: stats.totalDiscussions.toLocaleString(),
      icon: MessageSquare,
    },
    {
      title: 'Pending Reports',
      value: stats.pendingReports.toLocaleString(),
      icon: Flag,
      highlight: stats.pendingReports > 0,
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {cards.map((card) => (
        <Card key={card.title} className={card.highlight ? 'border-red-500' : ''}>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              {card.title}
            </CardTitle>
            <card.icon className="w-4 h-4 text-gray-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{card.value}</div>
            {card.trend && (
              <p className="text-xs text-green-500 mt-1">{card.trend}</p>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
```

### Activity Feed with Real-time Updates (Client Component)

**File:** `src/components/client/dashboard/ActivityFeed.tsx`

```typescript
'use client'

import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useSignalR } from '@/hooks/useSignalR';
import { ScrollArea } from '@/components/ui/scroll-area';

interface Activity {
  id: string;
  type: 'user_registered' | 'report_created' | 'discussion_created';
  message: string;
  timestamp: string;
  userId?: string;
}

interface ActivityFeedProps {
  initialActivity: Activity[];
}

export function ActivityFeed({ initialActivity }: ActivityFeedProps) {
  const [activities, setActivities] = useState(initialActivity);
  const connection = useSignalR();

  useEffect(() => {
    if (!connection) return;

    // Listen for real-time activity updates
    connection.on('ActivityCreated', (activity: Activity) => {
      setActivities((prev) => [activity, ...prev].slice(0, 20));
    });

    return () => {
      connection.off('ActivityCreated');
    };
  }, [connection]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Recent Activity</CardTitle>
      </CardHeader>
      <CardContent>
        <ScrollArea className="h-[400px]">
          <div className="space-y-3">
            {activities.map((activity) => (
              <div
                key={activity.id}
                className="flex items-start gap-3 p-3 rounded-lg hover:bg-gray-50 transition-colors"
              >
                <div className="flex-1">
                  <p className="text-sm">{activity.message}</p>
                  <p className="text-xs text-gray-500 mt-1">
                    {new Date(activity.timestamp).toLocaleString()}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
```

### SignalR Provider (Client Component)

**File:** `src/components/providers/SignalRProvider.tsx`

```typescript
'use client'

import { createContext, useContext, useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

const SignalRContext = createContext<signalR.HubConnection | null>(null);

export function SignalRProvider({ children }: { children: React.ReactNode }) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_SIGNALR_URL}/realtime`)
      .withAutomaticReconnect()
      .build();

    conn.start()
      .then(() => {
        console.log('SignalR connected');
        setConnection(conn);
      })
      .catch(err => console.error('SignalR connection error:', err));

    return () => {
      conn.stop();
    };
  }, []);

  return (
    <SignalRContext.Provider value={connection}>
      {children}
    </SignalRContext.Provider>
  );
}

export function useSignalR() {
  return useContext(SignalRContext);
}
```

---

## Phase 3: User Management

### Users List Page (Server Component)

**File:** `src/app/(dashboard)/users/page.tsx`

```typescript
import { Suspense } from 'react';
import { UserTable } from '@/components/client/users/UserTable';
import { getUsers } from '@/lib/api/users';
import { UserTableSkeleton } from '@/components/client/users/UserTableSkeleton';

interface PageProps {
  searchParams: {
    page?: string;
    search?: string;
    role?: string;
    status?: string;
  };
}

export default async function UsersPage({ searchParams }: PageProps) {
  const page = parseInt(searchParams.page || '1');
  const search = searchParams.search || '';
  const role = searchParams.role || '';
  const status = searchParams.status || '';

  // Server-side data fetching
  const data = await getUsers({
    page,
    search,
    role,
    status,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Users</h1>
      </div>

      <Suspense fallback={<UserTableSkeleton />}>
        <UserTable initialData={data} />
      </Suspense>
    </div>
  );
}
```

### User Table (Client Component)

**File:** `src/components/client/users/UserTable.tsx`

```typescript
'use client'

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
  ColumnDef,
  flexRender,
  getCoreRowModel,
  useReactTable,
  getSortedRowModel,
  SortingState,
} from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { getUsers } from '@/lib/api/users';

interface User {
  id: string;
  displayName: string;
  email: string;
  role: string;
  status: string;
  createdAt: string;
  lastActive: string;
}

interface UserTableProps {
  initialData: {
    users: User[];
    total: number;
    page: number;
    pageSize: number;
  };
}

export function UserTable({ initialData }: UserTableProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [page, setPage] = useState(parseInt(searchParams.get('page') || '1'));

  // Use TanStack Query for subsequent fetches
  const { data, isLoading } = useQuery({
    queryKey: ['users', page, search, searchParams.get('role'), searchParams.get('status')],
    queryFn: () => getUsers({
      page,
      search,
      role: searchParams.get('role') || '',
      status: searchParams.get('status') || '',
    }),
    initialData,
    staleTime: 30000, // 30 seconds
  });

  const columns: ColumnDef<User>[] = [
    {
      accessorKey: 'displayName',
      header: 'Name',
      cell: ({ row }) => (
        <div>
          <p className="font-medium">{row.original.displayName}</p>
          <p className="text-sm text-gray-500">{row.original.email}</p>
        </div>
      ),
    },
    {
      accessorKey: 'role',
      header: 'Role',
    },
    {
      accessorKey: 'status',
      header: 'Status',
      cell: ({ row }) => (
        <span
          className={`px-2 py-1 rounded text-xs ${
            row.original.status === 'active'
              ? 'bg-green-100 text-green-800'
              : 'bg-red-100 text-red-800'
          }`}
        >
          {row.original.status}
        </span>
      ),
    },
    {
      accessorKey: 'createdAt',
      header: 'Joined',
      cell: ({ row }) => new Date(row.original.createdAt).toLocaleDateString(),
    },
    {
      accessorKey: 'lastActive',
      header: 'Last Active',
      cell: ({ row }) => new Date(row.original.lastActive).toLocaleDateString(),
    },
  ];

  const table = useReactTable({
    data: data.users,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    onSortingChange: setSorting,
    state: {
      sorting,
    },
  });

  const handleSearch = (value: string) => {
    setSearch(value);
    setPage(1);
    const params = new URLSearchParams(searchParams);
    params.set('search', value);
    params.set('page', '1');
    router.push(`/users?${params.toString()}`);
  };

  return (
    <div className="space-y-4">
      {/* Search */}
      <Input
        placeholder="Search users..."
        value={search}
        onChange={(e) => handleSearch(e.target.value)}
        className="max-w-sm"
      />

      {/* Table */}
      <div className="rounded-md border">
        <table className="w-full">
          <thead>
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id} className="border-b bg-gray-50">
                {headerGroup.headers.map((header) => (
                  <th
                    key={header.id}
                    className="px-4 py-3 text-left text-sm font-medium"
                  >
                    {flexRender(
                      header.column.columnDef.header,
                      header.getContext()
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => (
              <tr key={row.id} className="border-b hover:bg-gray-50">
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id} className="px-4 py-3 text-sm">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">
          Showing {(page - 1) * data.pageSize + 1} to{' '}
          {Math.min(page * data.pageSize, data.total)} of {data.total} users
        </p>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage(page - 1)}
            disabled={page === 1}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage(page + 1)}
            disabled={page * data.pageSize >= data.total}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}
```

### User Detail Page (Server Component)

**File:** `src/app/(dashboard)/users/[id]/page.tsx`

```typescript
import { notFound } from 'next/navigation';
import { getUser, getUserActivity, getUserStats } from '@/lib/api/users';
import { UserProfile } from '@/components/server/users/UserProfile';
import { UserActivityTimeline } from '@/components/client/users/UserActivityTimeline';
import { UserActions } from '@/components/client/users/UserDetailActions';
import { Suspense } from 'react';

interface PageProps {
  params: {
    id: string;
  };
}

export default async function UserDetailPage({ params }: PageProps) {
  // Parallel data fetching
  const [user, activity, stats] = await Promise.all([
    getUser(params.id),
    getUserActivity(params.id),
    getUserStats(params.id),
  ]);

  if (!user) {
    notFound();
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">{user.displayName}</h1>
        <UserActions user={user} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left column - User profile (Server Component) */}
        <div className="lg:col-span-1">
          <UserProfile user={user} stats={stats} />
        </div>

        {/* Right column - Activity timeline (Client Component) */}
        <div className="lg:col-span-2">
          <Suspense fallback={<div>Loading activity...</div>}>
            <UserActivityTimeline
              userId={params.id}
              initialActivity={activity}
            />
          </Suspense>
        </div>
      </div>
    </div>
  );
}
```

---

## Data Fetching Strategy

### API Client Implementation

**File:** `src/lib/api/client.ts`

```typescript
import { cookies } from 'next/headers';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

async function getAuthToken() {
  // Server-side: get from cookies
  if (typeof window === 'undefined') {
    const cookieStore = await cookies();
    return cookieStore.get('admin_token')?.value;
  }

  // Client-side: token is in HTTP-only cookie, sent automatically
  return null;
}

export async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const token = await getAuthToken();

  const response = await fetch(`${API_URL}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token && { Authorization: `Bearer ${token}` }),
      ...options.headers,
    },
    credentials: 'include', // Important for cookies
  });

  if (!response.ok) {
    throw new Error(`API Error: ${response.statusText}`);
  }

  return response.json();
}
```

**File:** `src/lib/api/users.ts`

```typescript
import { apiRequest } from './client';

export async function getUsers(params: {
  page: number;
  search?: string;
  role?: string;
  status?: string;
}) {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    ...(params.search && { search: params.search }),
    ...(params.role && { role: params.role }),
    ...(params.status && { status: params.status }),
  });

  return apiRequest(`/api/admin/users?${queryParams}`);
}

export async function getUser(id: string) {
  return apiRequest(`/api/admin/users/${id}`);
}

export async function getUserActivity(id: string) {
  return apiRequest(`/api/admin/users/${id}/activity`);
}

export async function getUserStats(id: string) {
  return apiRequest(`/api/admin/users/${id}/stats`);
}

export async function banUser(id: string, reason: string, duration?: number) {
  return apiRequest(`/api/admin/users/${id}/ban`, {
    method: 'POST',
    body: JSON.stringify({ reason, duration }),
  });
}
```

---

## Decision Tree Reference

### Component Type Decision Flow

```
┌─────────────────────────────────────┐
│ Does it need interactivity?         │
│ (clicks, inputs, state changes)     │
└───────────┬─────────────────────────┘
            │
    ┌───────┴───────┐
    │               │
   NO              YES
    │               │
    ▼               ▼
┌─────────┐    ┌──────────┐
│ SERVER  │    │ CLIENT   │
│ COMPONENT│    │ COMPONENT│
└─────────┘    └──────────┘
    │               │
    │               ├─ Uses 'use client'
    │               ├─ React hooks allowed
    ├─ async/await  ├─ Browser APIs allowed
    ├─ Direct DB    ├─ Event handlers
    ├─ Zero JS      └─ Context consumers
    └─ SEO friendly
```

### Specific Use Cases

**Server Components:**
- ✅ Page layouts
- ✅ Static navigation
- ✅ Initial data fetching
- ✅ SEO content
- ✅ User profile display (non-editable)
- ✅ Stats cards
- ✅ Breadcrumbs

**Client Components:**
- ✅ Forms with validation
- ✅ Tables with sorting/filtering
- ✅ Modals and dialogs
- ✅ Dropdowns and menus
- ✅ Real-time feeds
- ✅ Charts and visualizations
- ✅ Search inputs
- ✅ Notifications

**Hybrid Pattern:**
- Server Component fetches data
- Passes to Client Component via props
- Client Component adds interactivity
- Uses TanStack Query for subsequent fetches

---

## Key Patterns

### Pattern 1: Server Fetches, Client Displays

```typescript
// Server Component (page)
export default async function Page() {
  const data = await fetchData();
  return <ClientTable initialData={data} />;
}

// Client Component
'use client'
export function ClientTable({ initialData }) {
  const { data } = useQuery({
    queryKey: ['data'],
    queryFn: fetchData,
    initialData, // Use server data initially
  });
  // ... interactive table
}
```

### Pattern 2: Composition

```typescript
// Server Component (layout)
export default function Layout({ children }) {
  return (
    <div>
      <StaticHeader /> {/* Server */}
      <InteractiveNav /> {/* Client */}
      {children}
    </div>
  );
}
```

### Pattern 3: Real-time Updates

```typescript
// Client Component only
'use client'
export function Feed({ initialData }) {
  const [items, setItems] = useState(initialData);
  const connection = useSignalR();

  useEffect(() => {
    connection?.on('NewItem', (item) => {
      setItems(prev => [item, ...prev]);
    });
  }, [connection]);

  return <div>{/* render items */}</div>;
}
```

---

## Common Pitfalls to Avoid

### ❌ Don't: Make entire pages client components

```typescript
// Bad
'use client'
export default function UsersPage() {
  // Entire page runs on client
}
```

### ✅ Do: Push 'use client' down

```typescript
// Good - Server Component page
export default async function UsersPage() {
  const data = await getUsers();
  return <UserTable initialData={data} />; // Client component
}
```

### ❌ Don't: Import Server Components into Client

```typescript
// Bad
'use client'
import { ServerComponent } from './ServerComponent';
// ServerComponent becomes client component!
```

### ✅ Do: Compose via children/props

```typescript
// Good - Server Component
export default function Page() {
  return (
    <ClientWrapper>
      <ServerComponent /> {/* Stays server */}
    </ClientWrapper>
  );
}
```

### ❌ Don't: Pass non-serializable props

```typescript
// Bad - Server to Client
<ClientComponent onSave={() => db.save()} /> // Error!
```

### ✅ Do: Pass data, handle actions in client

```typescript
// Good
<ClientComponent userId={user.id} />

// In client component
const handleSave = () => {
  fetch('/api/save', { body: JSON.stringify({ userId }) });
};
```

---

## Performance Considerations

### Server Components Benefits

1. **Bundle Size**: Zero JavaScript for Server Components
2. **Data Fetching**: Closer to data source (database, APIs)
3. **Security**: Sensitive logic stays on server
4. **SEO**: Full HTML rendered on server
5. **Caching**: Can cache at CDN/server level

### Client Components Best Practices

1. **Code Splitting**: Automatically split at 'use client' boundaries
2. **Lazy Loading**: Use dynamic imports for heavy components
3. **Memoization**: Use React.memo for expensive renders
4. **TanStack Query**: Handles caching, deduplication, background updates
5. **Initial Data**: Pass server data to avoid waterfall fetches

---

## Next Steps

### Immediate (Phase 1)
1. Set up Next.js project structure
2. Implement authentication system
3. Create root layout with providers
4. Build login page and form

### Short-term (Phase 2-3)
1. Implement dashboard layout
2. Create dashboard page with stats
3. Build user management pages
4. Set up SignalR for real-time features

### Medium-term (Phase 4+)
1. Content management (communities, hubs, spaces)
2. Moderation queue and tools
3. Analytics and reporting
4. Settings and configuration

---

**Document Version:** 1.0
**Last Updated:** 2026-02-06
**Author:** Implementation Guide
**Related Documents:**
- [admin-panel-architecture.md](./admin-panel-architecture.md) - Full feature specification
- [avatar-generation-disk-storage.md](./avatar-generation-disk-storage.md) - Avatar system implementation
