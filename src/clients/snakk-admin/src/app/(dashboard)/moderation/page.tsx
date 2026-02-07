import { Suspense } from 'react';
import { ModerationTabs } from '@/components/client/moderation/ModerationTabs';
import { getReports, getModerationLog, getActiveBans } from '@/lib/api/moderation';
import { Card, CardContent } from '@/components/ui/card';

function ModerationSkeleton() {
  return (
    <Card>
      <CardContent className="p-6">
        <div className="animate-pulse space-y-4">
          <div className="h-10 bg-gray-200 rounded w-1/3" />
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="h-16 bg-gray-200 rounded" />
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default async function ModerationPage() {
  // Fetch initial data server-side
  const [reportsData, logData, bansData] = await Promise.all([
    getReports({ page: 1 }),
    getModerationLog({ page: 1 }),
    getActiveBans({ page: 1 }),
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Moderation</h1>
        <p className="text-gray-500 mt-1">Review reports, manage bans, and view audit logs</p>
      </div>

      <Suspense fallback={<ModerationSkeleton />}>
        <ModerationTabs
          initialReports={reportsData}
          initialLog={logData}
          initialBans={bansData}
        />
      </Suspense>
    </div>
  );
}
