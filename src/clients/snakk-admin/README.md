# Snakk Admin Panel

Modern admin panel for the Snakk forum platform, built with Next.js 14 and the App Router.

## Tech Stack

- **Framework:** Next.js 14 (App Router)
- **Language:** TypeScript
- **Styling:** Tailwind CSS
- **UI Components:** Shadcn/ui (built on Radix UI)
- **State Management:** Zustand (auth state)
- **Data Fetching:** TanStack Query (React Query)
- **Authentication:** JWT with HTTP-only cookies
- **Toast Notifications:** Sonner
- **Real-time:** SignalR (planned)

## Features (Phase 1 Complete)

- Modern Next.js 14 with App Router architecture
- Server Components by default for optimal performance
- Complete authentication system:
  - JWT token verification on server
  - HTTP-only cookie-based sessions
  - Protected routes via middleware
  - Client-side auth state management
- Login page with form validation
- Dashboard with placeholder metrics
- Shadcn/ui component library integration
- TanStack Query setup for data fetching
- Toast notifications with Sonner
- TypeScript throughout
- Tailwind CSS styling

## Getting Started

### Prerequisites

- Node.js 18+ installed
- npm, yarn, pnpm, or bun package manager
- Running Snakk API backend (default: http://localhost:5000)

### Installation

1. Install dependencies:

```bash
npm install
# or
yarn install
# or
pnpm install
```

2. Create `.env.local` file in the project root:

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXT_PUBLIC_SIGNALR_URL=http://localhost:5000
JWT_SECRET=your-secret-key-change-this-in-production
NEXT_PUBLIC_GA_ID=
```

3. Run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
```

4. Open [http://localhost:3000](http://localhost:3000) in your browser.

### Building for Production

```bash
npm run build
npm run start
```

## Project Structure

```
src/clients/snakk-admin/
├── src/
│   ├── app/                    # App Router pages and layouts
│   │   ├── (auth)/            # Auth route group
│   │   │   └── login/         # Login page
│   │   ├── layout.tsx         # Root layout with providers
│   │   └── page.tsx           # Dashboard page
│   ├── components/
│   │   ├── auth/              # Auth-related components
│   │   │   └── LoginForm.tsx  # Login form (client component)
│   │   ├── providers/         # Context providers
│   │   │   └── Providers.tsx  # React Query & toast providers
│   │   └── ui/                # Shadcn/ui components
│   ├── lib/
│   │   ├── auth/
│   │   │   ├── client.ts      # Client-side auth utilities
│   │   │   └── server.ts      # Server-side auth utilities
│   │   ├── api/
│   │   │   └── client.ts      # API client with error handling
│   │   └── utils.ts           # Utility functions
│   └── middleware.ts          # Route protection middleware
├── public/                     # Static assets
├── .env.local                 # Environment variables
└── package.json
```

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `NEXT_PUBLIC_API_URL` | Snakk API base URL | Yes |
| `NEXT_PUBLIC_SIGNALR_URL` | SignalR hub URL | Yes |
| `JWT_SECRET` | Secret for JWT verification | Yes |
| `NEXT_PUBLIC_GA_ID` | Google Analytics ID | No |

## Architecture Decisions

### Server vs Client Components

- **Server Components (default):** Used for pages, layouts, and non-interactive UI
  - Async/await support for direct data fetching
  - Zero JavaScript sent to client
  - Better SEO and initial load performance

- **Client Components ('use client'):** Used for interactivity and state
  - Forms, buttons with onClick handlers
  - React hooks (useState, useEffect, etc.)
  - Browser APIs and event listeners
  - Zustand stores and TanStack Query hooks

### Authentication Flow

1. **Middleware** (`middleware.ts`): Edge runtime route protection
   - Checks for `admin_token` cookie
   - Redirects unauthenticated users to /login
   - Redirects authenticated users away from /login

2. **Server-side** (`lib/auth/server.ts`): JWT verification
   - Uses jose library for JWT verification
   - `getServerSession()`: Returns session or null
   - `requireAuth()`: Throws if not authenticated
   - Used in Server Components and API routes

3. **Client-side** (`lib/auth/client.ts`): State management
   - Zustand store for user state
   - Login/logout functions
   - Used in Client Components for UI updates

### Data Fetching Strategy

- **Initial Load:** Server Components fetch data directly
- **Client-side Updates:** TanStack Query for caching, refetching, and optimistic updates
- **Real-time:** SignalR for live updates (planned for Phase 5)

## Security Considerations

- JWT tokens stored in HTTP-only cookies (not localStorage)
- Server-side token verification on all protected routes
- Middleware runs on Edge Runtime for fast auth checks
- API requests include credentials for cookie passing
- CSRF protection via SameSite cookie attribute (configured in API)

## Development Commands

```bash
npm run dev          # Start development server
npm run build        # Build for production
npm run start        # Start production server
npm run lint         # Run ESLint
```

## Next Steps (Phase 2)

- [ ] Dashboard layout with sidebar navigation
- [ ] Top navbar with user menu
- [ ] Breadcrumb navigation
- [ ] User management pages (list, view, edit)
- [ ] Create admin API endpoints in Snakk.Api
- [ ] Integrate real data fetching with TanStack Query
- [ ] Add loading states and error boundaries
- [ ] Implement form validation with Zod

## Contributing

This is an internal admin panel for the Snakk platform. For questions or issues, contact the development team.

## License

Proprietary - All rights reserved
