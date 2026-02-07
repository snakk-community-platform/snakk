'use client'

import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import type { UserActivity } from '@/lib/api/users';
import { MessageSquare, FileText, Heart, MessageCircle } from 'lucide-react';

interface UserActivityTimelineProps {
  userId: string;
  initialActivity: UserActivity[];
}

const activityIcons = {
  post_created: MessageSquare,
  discussion_created: FileText,
  reaction_added: Heart,
  comment_added: MessageCircle,
};

const activityColors = {
  post_created: 'text-blue-500 bg-blue-100',
  discussion_created: 'text-green-500 bg-green-100',
  reaction_added: 'text-red-500 bg-red-100',
  comment_added: 'text-purple-500 bg-purple-100',
};

export function UserActivityTimeline({ userId, initialActivity }: UserActivityTimelineProps) {
  const [activities] = useState(initialActivity);

  function formatTimeAgo(timestamp: string): string {
    const now = new Date();
    const activityTime = new Date(timestamp);
    const diffMs = now.getTime() - activityTime.getTime();
    const diffMins = Math.floor(diffMs / (1000 * 60));

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;

    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;

    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) return `${diffDays}d ago`;

    return new Date(timestamp).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Activity Timeline</CardTitle>
      </CardHeader>
      <CardContent>
        <ScrollArea className="h-[600px] pr-4">
          {activities.length === 0 ? (
            <div className="flex items-center justify-center h-full text-gray-400">
              No activity yet
            </div>
          ) : (
            <div className="space-y-4">
              {activities.map((activity, index) => {
                const Icon = activityIcons[activity.type];
                const colorClass = activityColors[activity.type];

                return (
                  <div key={activity.id} className="flex gap-4">
                    {/* Timeline line */}
                    <div className="flex flex-col items-center">
                      <div className={`w-10 h-10 rounded-full ${colorClass} flex items-center justify-center`}>
                        <Icon className="w-5 h-5" />
                      </div>
                      {index < activities.length - 1 && (
                        <div className="w-0.5 flex-1 bg-gray-200 my-2" />
                      )}
                    </div>

                    {/* Activity content */}
                    <div className="flex-1 pb-8">
                      <p className="text-sm text-gray-900 mb-1">
                        {activity.description}
                      </p>
                      <p className="text-xs text-gray-500">
                        {formatTimeAgo(activity.timestamp)}
                      </p>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
