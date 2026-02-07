'use client'

import { useState } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AuditLogTable } from './AuditLogTable';
import { SecurityMonitoring } from './SecurityMonitoring';
import { ComplianceTools } from './ComplianceTools';
import { LiveActivityFeed } from './LiveActivityFeed';
import type {
  AuditLogsResponse,
  FailedLoginsResponse,
  ActiveSessionsResponse,
  SuspiciousActivitiesResponse,
} from '@/lib/api/security';

interface SecurityTabsProps {
  initialAuditLogs: AuditLogsResponse;
  initialFailedLogins: FailedLoginsResponse;
  initialSessions: ActiveSessionsResponse;
  initialActivities: SuspiciousActivitiesResponse;
}

export function SecurityTabs({
  initialAuditLogs,
  initialFailedLogins,
  initialSessions,
  initialActivities,
}: SecurityTabsProps) {
  const [activeTab, setActiveTab] = useState('live');

  return (
    <Tabs value={activeTab} onValueChange={setActiveTab}>
      <TabsList className="grid w-full grid-cols-4">
        <TabsTrigger value="live">Live Activity</TabsTrigger>
        <TabsTrigger value="audit">Audit Logs</TabsTrigger>
        <TabsTrigger value="security">Security</TabsTrigger>
        <TabsTrigger value="compliance">Compliance</TabsTrigger>
      </TabsList>

      <TabsContent value="live" className="mt-6">
        <LiveActivityFeed />
      </TabsContent>

      <TabsContent value="audit" className="mt-6">
        <AuditLogTable initialData={initialAuditLogs} />
      </TabsContent>

      <TabsContent value="security" className="mt-6">
        <SecurityMonitoring
          initialFailedLogins={initialFailedLogins}
          initialSessions={initialSessions}
          initialActivities={initialActivities}
        />
      </TabsContent>

      <TabsContent value="compliance" className="mt-6">
        <ComplianceTools />
      </TabsContent>
    </Tabs>
  );
}
