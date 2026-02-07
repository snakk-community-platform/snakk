import { Suspense } from 'react';
import { ContentOverview } from '@/components/server/content/ContentOverview';
import { ContentTabs } from '@/components/client/content/ContentTabs';
import { getCommunities, getHubs, getSpaces, getDiscussions } from '@/lib/api/content';

export default async function ContentPage() {
  // Fetch initial data server-side
  const [communitiesData, hubsData, spacesData, discussionsData] = await Promise.all([
    getCommunities({ page: 1 }),
    getHubs({ page: 1 }),
    getSpaces({ page: 1 }),
    getDiscussions({ page: 1 }),
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Content Management</h1>
        <p className="text-gray-500 mt-1">Manage communities, hubs, spaces, and discussions</p>
      </div>

      <Suspense fallback={<div>Loading overview...</div>}>
        <ContentOverview />
      </Suspense>

      <ContentTabs
        initialCommunities={communitiesData}
        initialHubs={hubsData}
        initialSpaces={spacesData}
        initialDiscussions={discussionsData}
      />
    </div>
  );
}
