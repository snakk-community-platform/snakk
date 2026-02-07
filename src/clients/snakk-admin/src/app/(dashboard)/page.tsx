import { Suspense } from 'react';
import { StatsCards } from '@/components/server/dashboard/StatsCards';
import { ActivityFeed } from '@/components/client/dashboard/ActivityFeed';
import { getStats, getRecentActivity } from '@/lib/api/dashboard';
import { Card, CardContent } from '@/components/ui/card';

function StatsCardsSkeleton() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {[...Array(4)].map((_, i) => (
        <Card key={i} className="animate-pulse">
          <CardContent className="h-24" />
        </Card>
      ))}
    </div>
  );
}

export default async function DashboardPage() {
  // These fetch in parallel on the server
  const [stats, recentActivity] = await Promise.all([
    getStats(),
    getRecentActivity(),
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-gray-500 mt-1">Welcome to the Snakk admin panel</p>
      </div>

      {/* Server Component - stats cards */}
      <Suspense fallback={<StatsCardsSkeleton />}>
        <StatsCards stats={stats} />
      </Suspense>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Client Component - real-time activity feed */}
        <ActivityFeed initialActivity={recentActivity} />

        {/* Placeholder for charts */}
        <Card>
          <CardContent className="p-6">
            <h3 className="text-lg font-semibold mb-4">Analytics</h3>
            <div className="h-64 flex items-center justify-center text-gray-400">
              Chart placeholder - Coming soon
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
