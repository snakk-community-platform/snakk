'use client'

import { useState } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ReportsTable } from './ReportsTable';
import { ModerationLogTable } from './ModerationLogTable';
import { BansTable } from './BansTable';
import type { ReportsResponse, ModerationLogResponse, BansResponse } from '@/lib/api/moderation';

interface ModerationTabsProps {
  initialReports: ReportsResponse;
  initialLog: ModerationLogResponse;
  initialBans: BansResponse;
}

export function ModerationTabs({
  initialReports,
  initialLog,
  initialBans,
}: ModerationTabsProps) {
  const [activeTab, setActiveTab] = useState('reports');

  return (
    <Tabs value={activeTab} onValueChange={setActiveTab}>
      <TabsList className="grid w-full grid-cols-3">
        <TabsTrigger value="reports">Reports</TabsTrigger>
        <TabsTrigger value="log">Moderation Log</TabsTrigger>
        <TabsTrigger value="bans">Active Bans</TabsTrigger>
      </TabsList>

      <TabsContent value="reports" className="mt-6">
        <ReportsTable initialData={initialReports} />
      </TabsContent>

      <TabsContent value="log" className="mt-6">
        <ModerationLogTable initialData={initialLog} />
      </TabsContent>

      <TabsContent value="bans" className="mt-6">
        <BansTable initialData={initialBans} />
      </TabsContent>
    </Tabs>
  );
}
