'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import { getHubs, type Hub, type HubsResponse } from '@/lib/api/content';
import { Search, Folder, MessageSquare } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

interface HubsTableProps {
  initialData: HubsResponse;
}

export function HubsTable({ initialData }: HubsTableProps) {
  const { toast } = useToast();
  const [hubs, setHubs] = useState<Hub[]>(initialData.hubs);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const loadHubs = async () => {
    setIsLoading(true);
    try {
      const data = await getHubs({ page, search: search || undefined });
      setHubs(data.hubs);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load hubs',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadHubs();
  }, [page, search]);

  const totalPages = Math.ceil(total / 20);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Hubs</CardTitle>
        <div className="flex items-center gap-4 mt-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
            <Input
              placeholder="Search hubs..."
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
          <div className="text-center py-8 text-gray-500">Loading hubs...</div>
        ) : hubs.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No hubs found</div>
        ) : (
          <div className="space-y-4">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">Name</th>
                    <th className="text-left p-3 font-medium text-gray-700">Slug</th>
                    <th className="text-left p-3 font-medium text-gray-700">Community</th>
                    <th className="text-left p-3 font-medium text-gray-700">Spaces</th>
                    <th className="text-left p-3 font-medium text-gray-700">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {hubs.map((hub) => (
                    <tr key={hub.id} className="border-b hover:bg-gray-50">
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          <Folder className="w-4 h-4 text-purple-600" />
                          <span className="font-medium text-gray-900">{hub.name}</span>
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600 font-mono">{hub.slug}</td>
                      <td className="p-3 text-sm text-gray-600">{hub.communityName}</td>
                      <td className="p-3">
                        <div className="flex items-center gap-1 text-sm text-gray-600">
                          <MessageSquare className="w-4 h-4" />
                          {hub.spaceCount}
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {formatDistanceToNow(new Date(hub.createdAt), { addSuffix: true })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-4">
                <div className="text-sm text-gray-600">
                  Showing {(page - 1) * 20 + 1}-{Math.min(page * 20, total)} of {total} hubs
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
