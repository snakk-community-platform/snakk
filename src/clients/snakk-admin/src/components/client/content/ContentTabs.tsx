'use client'

import { useState } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { CommunitiesTable } from './CommunitiesTable';
import { HubsTable } from './HubsTable';
import { SpacesTable } from './SpacesTable';
import { DiscussionsTable } from './DiscussionsTable';
import type { CommunitiesResponse, HubsResponse, SpacesResponse, DiscussionsResponse } from '@/lib/api/content';

interface ContentTabsProps {
  initialCommunities: CommunitiesResponse;
  initialHubs: HubsResponse;
  initialSpaces: SpacesResponse;
  initialDiscussions: DiscussionsResponse;
}

export function ContentTabs({
  initialCommunities,
  initialHubs,
  initialSpaces,
  initialDiscussions,
}: ContentTabsProps) {
  const [activeTab, setActiveTab] = useState('communities');

  return (
    <Tabs value={activeTab} onValueChange={setActiveTab}>
      <TabsList className="grid w-full grid-cols-4">
        <TabsTrigger value="communities">Communities</TabsTrigger>
        <TabsTrigger value="hubs">Hubs</TabsTrigger>
        <TabsTrigger value="spaces">Spaces</TabsTrigger>
        <TabsTrigger value="discussions">Discussions</TabsTrigger>
      </TabsList>

      <TabsContent value="communities" className="mt-6">
        <CommunitiesTable initialData={initialCommunities} />
      </TabsContent>

      <TabsContent value="hubs" className="mt-6">
        <HubsTable initialData={initialHubs} />
      </TabsContent>

      <TabsContent value="spaces" className="mt-6">
        <SpacesTable initialData={initialSpaces} />
      </TabsContent>

      <TabsContent value="discussions" className="mt-6">
        <DiscussionsTable initialData={initialDiscussions} />
      </TabsContent>
    </Tabs>
  );
}
