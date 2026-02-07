import { requireAuth } from '@/lib/auth/server';
import { Sidebar } from '@/components/server/layout/Sidebar';
import { Navbar } from '@/components/client/layout/Navbar';

export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // This runs on server - if not authenticated, middleware redirects
  const session = await requireAuth();

  return (
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
  );
}
