'use client'

import { useState, useEffect, useRef } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Play,
  Pause,
  Trash2,
  Download,
  MessageSquare,
  Heart,
  UserPlus,
  FileText,
  Shield,
  Users,
  Hash,
} from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import { ScrollArea } from '@/components/ui/scroll-area';

interface ActivityEvent {
  id: string;
  type: string; // 'post', 'reaction', 'follow', 'discussion', 'moderation', etc.
  category: string; // 'content', 'social', 'moderation', 'user'
  userId: string;
  username: string;
  targetId?: string;
  targetType?: string; // 'user', 'post', 'discussion', 'community', 'space'
  targetName?: string;
  communityId?: string;
  communityName?: string;
  spaceId?: string;
  spaceName?: string;
  hubId?: string;
  hubName?: string;
  action: string; // Human-readable action description
  details?: string;
  timestamp: string;
}

export function LiveActivityFeed() {
  const [events, setEvents] = useState<ActivityEvent[]>([]);
  const [isPaused, setIsPaused] = useState(false);
  const [autoScroll, setAutoScroll] = useState(true);
  const [filterType, setFilterType] = useState<string>('all');
  const [filterCategory, setFilterCategory] = useState<string>('all');
  const [filterUser, setFilterUser] = useState<string>('');
  const [filterCommunity, setFilterCommunity] = useState<string>('');
  const [filterSpace, setFilterSpace] = useState<string>('');
  const [maxEvents] = useState(500); // Keep last 500 events in memory
  const scrollRef = useRef<HTMLDivElement>(null);
  const connectionRef = useRef<any>(null);

  useEffect(() => {
    const initializeSignalR = async () => {
      try {
        // Dynamic import to avoid SSR issues
        const signalR = await import('@microsoft/signalr');

        const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5242';
        const connection = new signalR.HubConnectionBuilder()
          .withUrl(`${apiUrl}/hubs/activity`, {
            withCredentials: true, // Include cookies for authentication
          })
          .withAutomaticReconnect()
          .configureLogging(signalR.LogLevel.Information)
          .build();

        // Listen for all activity events (backend sends them all as "ActivityEvent")
        connection.on('ActivityEvent', (event: any) => {
          if (!isPaused) {
            // Convert backend event to frontend format
            const activityEvent: ActivityEvent = {
              id: event.id,
              type: event.type,
              category: event.category,
              userId: event.userId,
              username: event.username,
              targetId: event.targetId,
              targetType: event.targetType,
              targetName: event.targetName,
              communityId: event.communityId,
              communityName: event.communityName,
              spaceId: event.spaceId,
              spaceName: event.spaceName,
              hubId: event.hubId,
              hubName: event.hubName,
              action: event.action,
              details: event.details,
              timestamp: event.timestamp,
            };
            addEvent(activityEvent);
          }
        });

        await connection.start();
        console.log('SignalR connected to activity hub');
        connectionRef.current = connection;
      } catch (error) {
        console.error('SignalR connection error:', error);
        console.log('Running without real-time updates');
      }
    };

    initializeSignalR();

    return () => {
      connectionRef.current?.stop();
    };
  }, [isPaused]);

  useEffect(() => {
    if (autoScroll && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [events, autoScroll]);

  const addEvent = (event: ActivityEvent) => {
    setEvents((prev) => {
      const updated = [event, ...prev];
      return updated.slice(0, maxEvents);
    });
  };

  const clearEvents = () => {
    setEvents([]);
  };

  const exportEvents = () => {
    const data = JSON.stringify(filteredEvents, null, 2);
    const blob = new Blob([data], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `activity-feed-${new Date().toISOString()}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const filteredEvents = events.filter((event) => {
    if (filterType !== 'all' && event.type !== filterType) return false;
    if (filterCategory !== 'all' && event.category !== filterCategory) return false;
    if (filterUser && !event.username.toLowerCase().includes(filterUser.toLowerCase())) return false;
    if (filterCommunity && !event.communityName?.toLowerCase().includes(filterCommunity.toLowerCase())) return false;
    if (filterSpace && !event.spaceName?.toLowerCase().includes(filterSpace.toLowerCase())) return false;
    return true;
  });

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'post':
        return <MessageSquare className="w-4 h-4 text-blue-600" />;
      case 'reaction':
        return <Heart className="w-4 h-4 text-red-600" />;
      case 'follow':
        return <UserPlus className="w-4 h-4 text-green-600" />;
      case 'discussion':
        return <FileText className="w-4 h-4 text-purple-600" />;
      case 'moderation':
        return <Shield className="w-4 h-4 text-orange-600" />;
      default:
        return <Hash className="w-4 h-4 text-gray-600" />;
    }
  };

  return (
    <Card className="h-[800px] flex flex-col">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle>Live Activity Feed</CardTitle>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setIsPaused(!isPaused)}
            >
              {isPaused ? (
                <>
                  <Play className="w-4 h-4 mr-2" />
                  Resume
                </>
              ) : (
                <>
                  <Pause className="w-4 h-4 mr-2" />
                  Pause
                </>
              )}
            </Button>
            <Button variant="outline" size="sm" onClick={clearEvents}>
              <Trash2 className="w-4 h-4 mr-2" />
              Clear
            </Button>
            <Button variant="outline" size="sm" onClick={exportEvents}>
              <Download className="w-4 h-4 mr-2" />
              Export
            </Button>
          </div>
        </div>

        {/* Filters */}
        <div className="grid grid-cols-2 md:grid-cols-5 gap-2 mt-4">
          <Select value={filterType} onValueChange={setFilterType}>
            <SelectTrigger>
              <SelectValue placeholder="All types" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All types</SelectItem>
              <SelectItem value="post">Posts</SelectItem>
              <SelectItem value="reaction">Reactions</SelectItem>
              <SelectItem value="follow">Follows</SelectItem>
              <SelectItem value="discussion">Discussions</SelectItem>
              <SelectItem value="moderation">Moderation</SelectItem>
              <SelectItem value="user">User Actions</SelectItem>
            </SelectContent>
          </Select>

          <Select value={filterCategory} onValueChange={setFilterCategory}>
            <SelectTrigger>
              <SelectValue placeholder="All categories" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All categories</SelectItem>
              <SelectItem value="content">Content</SelectItem>
              <SelectItem value="social">Social</SelectItem>
              <SelectItem value="moderation">Moderation</SelectItem>
              <SelectItem value="user">User</SelectItem>
            </SelectContent>
          </Select>

          <Input
            placeholder="Filter by user..."
            value={filterUser}
            onChange={(e) => setFilterUser(e.target.value)}
          />

          <Input
            placeholder="Filter by community..."
            value={filterCommunity}
            onChange={(e) => setFilterCommunity(e.target.value)}
          />

          <Input
            placeholder="Filter by space..."
            value={filterSpace}
            onChange={(e) => setFilterSpace(e.target.value)}
          />
        </div>

        <div className="flex items-center justify-between mt-2 text-sm">
          <div className="text-gray-600">
            {filteredEvents.length} events {events.length !== filteredEvents.length && `(${events.length} total)`}
          </div>
          <label className="flex items-center gap-2 text-gray-600">
            <input
              type="checkbox"
              checked={autoScroll}
              onChange={(e) => setAutoScroll(e.target.checked)}
              className="rounded"
            />
            Auto-scroll
          </label>
        </div>
      </CardHeader>

      <CardContent className="flex-1 overflow-hidden p-0">
        <ScrollArea className="h-full px-6 pb-6" ref={scrollRef}>
          <div className="space-y-2">
            {filteredEvents.length === 0 ? (
              <div className="text-center py-8 text-gray-500">
                {isPaused ? 'Feed paused - click Resume to continue' : 'Waiting for activity...'}
              </div>
            ) : (
              filteredEvents.map((event) => (
                <div
                  key={event.id}
                  className="flex items-start gap-3 p-3 border rounded-lg hover:bg-gray-50 transition-colors animate-in fade-in slide-in-from-top-2 duration-300"
                >
                  <div className="mt-0.5">{getEventIcon(event.type)}</div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex-1">
                        <div className="text-sm font-medium text-gray-900">
                          {event.username}
                        </div>
                        <div className="text-sm text-gray-600 mt-0.5">
                          {event.action}
                        </div>
                        {(event.communityName || event.spaceName) && (
                          <div className="flex items-center gap-2 mt-1 text-xs text-gray-500">
                            {event.communityName && (
                              <span className="inline-flex items-center gap-1">
                                <Users className="w-3 h-3" />
                                {event.communityName}
                              </span>
                            )}
                            {event.spaceName && (
                              <span className="inline-flex items-center gap-1">
                                <Hash className="w-3 h-3" />
                                {event.spaceName}
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                      <div className="text-xs text-gray-500 whitespace-nowrap">
                        {formatDistanceToNow(new Date(event.timestamp), { addSuffix: true })}
                      </div>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
