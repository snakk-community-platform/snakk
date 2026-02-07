'use client'

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useToast } from '@/hooks/use-toast';
import {
  getDiscussions,
  pinDiscussion,
  unpinDiscussion,
  lockDiscussion,
  unlockDiscussion,
  deleteDiscussion,
  type Discussion,
  type DiscussionsResponse,
} from '@/lib/api/content';
import { Search, MoreVertical, Pin, Lock, Unlock, Trash2, MessageSquare } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

interface DiscussionsTableProps {
  initialData: DiscussionsResponse;
}

export function DiscussionsTable({ initialData }: DiscussionsTableProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [discussions, setDiscussions] = useState<Discussion[]>(initialData.discussions);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const loadDiscussions = async () => {
    setIsLoading(true);
    try {
      const data = await getDiscussions({ page, search: search || undefined });
      setDiscussions(data.discussions);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load discussions',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadDiscussions();
  }, [page, search]);

  const handlePin = async (discussion: Discussion) => {
    try {
      if (discussion.isPinned) {
        await unpinDiscussion(discussion.id);
        toast({
          title: 'Discussion unpinned',
          description: `"${discussion.title}" has been unpinned`,
        });
      } else {
        await pinDiscussion(discussion.id);
        toast({
          title: 'Discussion pinned',
          description: `"${discussion.title}" has been pinned`,
        });
      }
      loadDiscussions();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to update discussion',
      });
    }
  };

  const handleLock = async (discussion: Discussion) => {
    try {
      if (discussion.isLocked) {
        await unlockDiscussion(discussion.id);
        toast({
          title: 'Discussion unlocked',
          description: `"${discussion.title}" has been unlocked`,
        });
      } else {
        await lockDiscussion(discussion.id);
        toast({
          title: 'Discussion locked',
          description: `"${discussion.title}" has been locked`,
        });
      }
      loadDiscussions();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to update discussion',
      });
    }
  };

  const handleDelete = async (discussion: Discussion) => {
    if (!confirm(`Are you sure you want to delete "${discussion.title}"? This action cannot be undone.`)) {
      return;
    }

    try {
      await deleteDiscussion(discussion.id);
      toast({
        title: 'Discussion deleted',
        description: `"${discussion.title}" has been deleted`,
      });
      loadDiscussions();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to delete discussion',
      });
    }
  };

  const totalPages = Math.ceil(total / 20);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Discussions</CardTitle>
        <div className="flex items-center gap-4 mt-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
            <Input
              placeholder="Search discussions..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="pl-10"
            />
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="text-center py-8 text-gray-500">Loading discussions...</div>
        ) : discussions.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No discussions found</div>
        ) : (
          <div className="space-y-4">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">Title</th>
                    <th className="text-left p-3 font-medium text-gray-700">Location</th>
                    <th className="text-left p-3 font-medium text-gray-700">Author</th>
                    <th className="text-left p-3 font-medium text-gray-700">Posts</th>
                    <th className="text-left p-3 font-medium text-gray-700">Status</th>
                    <th className="text-left p-3 font-medium text-gray-700">Created</th>
                    <th className="text-right p-3 font-medium text-gray-700">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {discussions.map((discussion) => (
                    <tr key={discussion.id} className="border-b hover:bg-gray-50">
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          {discussion.isPinned && <Pin className="w-4 h-4 text-indigo-600" />}
                          {discussion.isLocked && <Lock className="w-4 h-4 text-red-600" />}
                          <span className="font-medium text-gray-900">{discussion.title}</span>
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        <div>
                          {discussion.communityName} / {discussion.hubName} / {discussion.spaceName}
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">{discussion.authorName}</td>
                      <td className="p-3">
                        <div className="flex items-center gap-1 text-sm text-gray-600">
                          <MessageSquare className="w-4 h-4" />
                          {discussion.postCount}
                        </div>
                      </td>
                      <td className="p-3">
                        <div className="flex gap-1">
                          {discussion.isPinned && <Badge variant="secondary">Pinned</Badge>}
                          {discussion.isLocked && <Badge variant="destructive">Locked</Badge>}
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {formatDistanceToNow(new Date(discussion.createdAt), { addSuffix: true })}
                      </td>
                      <td className="p-3">
                        <div className="flex justify-end">
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon">
                                <MoreVertical className="w-4 h-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => handlePin(discussion)}>
                                <Pin className="w-4 h-4 mr-2" />
                                {discussion.isPinned ? 'Unpin' : 'Pin'} Discussion
                              </DropdownMenuItem>
                              <DropdownMenuItem onClick={() => handleLock(discussion)}>
                                {discussion.isLocked ? (
                                  <Unlock className="w-4 h-4 mr-2" />
                                ) : (
                                  <Lock className="w-4 h-4 mr-2" />
                                )}
                                {discussion.isLocked ? 'Unlock' : 'Lock'} Discussion
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem
                                onClick={() => handleDelete(discussion)}
                                className="text-red-600"
                              >
                                <Trash2 className="w-4 h-4 mr-2" />
                                Delete Discussion
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-4">
                <div className="text-sm text-gray-600">
                  Showing {(page - 1) * 20 + 1}-{Math.min(page * 20, total)} of {total} discussions
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage(page - 1)}
                    disabled={page === 1}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage(page + 1)}
                    disabled={page === totalPages}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
