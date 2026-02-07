import { Suspense } from 'react';
import { SecurityTabs } from '@/components/client/security/SecurityTabs';
import {
  getAuditLogs,
  getFailedLogins,
  getActiveSessions,
  getSuspiciousActivities,
} from '@/lib/api/security';
import { Card, CardContent } from '@/components/ui/card';

function SecuritySkeleton() {
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

export default async function SecurityPage() {
  // Fetch initial data server-side
  const [auditLogsData, failedLoginsData, sessionsData, activitiesData] = await Promise.all([
    getAuditLogs({ page: 1 }),
    getFailedLogins({ page: 1 }),
    getActiveSessions(),
    getSuspiciousActivities({ page: 1 }),
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Security & Audit</h1>
        <p className="text-gray-500 mt-1">
          Monitor platform security, audit logs, and compliance tools
        </p>
      </div>

      <Suspense fallback={<SecuritySkeleton />}>
        <SecurityTabs
          initialAuditLogs={auditLogsData}
          initialFailedLogins={failedLoginsData}
          initialSessions={sessionsData}
          initialActivities={activitiesData}
        />
      </Suspense>
    </div>
  );
}
