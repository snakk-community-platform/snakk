import { notFound } from 'next/navigation';
import { getUser, getUserActivity, getUserStats } from '@/lib/api/users';
import { UserProfile } from '@/components/server/users/UserProfile';
import { UserActivityTimeline } from '@/components/client/users/UserActivityTimeline';
import { UserActions } from '@/components/client/users/UserActions';
import { Suspense } from 'react';
import { Card, CardContent } from '@/components/ui/card';

interface PageProps {
  params: Promise<{
    id: string;
  }>;
}

export default async function UserDetailPage({ params }: PageProps) {
  const { id } = await params;

  // Parallel data fetching
  const [user, activity, stats] = await Promise.all([
    getUser(id),
    getUserActivity(id),
    getUserStats(id),
  ]);

  if (!user) {
    notFound();
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">{user.displayName}</h1>
          <p className="text-gray-500 mt-1">{user.email}</p>
        </div>
        <UserActions user={user} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left column - User profile (Server Component) */}
        <div className="lg:col-span-1">
          <UserProfile user={user} stats={stats} />
        </div>

        {/* Right column - Activity timeline (Client Component) */}
        <div className="lg:col-span-2">
          <Suspense fallback={
            <Card>
              <CardContent className="p-6">
                <div className="animate-pulse space-y-4">
                  {[...Array(5)].map((_, i) => (
                    <div key={i} className="h-20 bg-gray-100 rounded" />
                  ))}
                </div>
              </CardContent>
            </Card>
          }>
            <UserActivityTimeline
              userId={id}
              initialActivity={activity}
            />
          </Suspense>
        </div>
      </div>
    </div>
  );
}
