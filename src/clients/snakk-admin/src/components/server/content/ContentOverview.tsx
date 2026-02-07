import { getContentOverview } from '@/lib/api/content';
import { Card, CardContent } from '@/components/ui/card';
import { Building2, Folder, MessageSquare, FileText, Pin, Lock } from 'lucide-react';

export async function ContentOverview() {
  const overview = await getContentOverview();

  const stats = [
    {
      label: 'Communities',
      value: overview.totalCommunities,
      icon: Building2,
      color: 'text-blue-600',
      bgColor: 'bg-blue-50',
    },
    {
      label: 'Hubs',
      value: overview.totalHubs,
      icon: Folder,
      color: 'text-purple-600',
      bgColor: 'bg-purple-50',
    },
    {
      label: 'Spaces',
      value: overview.totalSpaces,
      icon: MessageSquare,
      color: 'text-green-600',
      bgColor: 'bg-green-50',
    },
    {
      label: 'Discussions',
      value: overview.totalDiscussions,
      icon: FileText,
      color: 'text-orange-600',
      bgColor: 'bg-orange-50',
    },
    {
      label: 'Pinned',
      value: overview.pinnedDiscussions,
      icon: Pin,
      color: 'text-indigo-600',
      bgColor: 'bg-indigo-50',
    },
    {
      label: 'Locked',
      value: overview.lockedDiscussions,
      icon: Lock,
      color: 'text-red-600',
      bgColor: 'bg-red-50',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
      {stats.map((stat) => (
        <Card key={stat.label}>
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-600">{stat.label}</p>
                <p className="text-2xl font-bold text-gray-900 mt-1">{stat.value.toLocaleString()}</p>
              </div>
              <div className={`p-3 rounded-lg ${stat.bgColor}`}>
                <stat.icon className={`w-6 h-6 ${stat.color}`} />
              </div>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
