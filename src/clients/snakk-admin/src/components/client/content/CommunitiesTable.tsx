'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/hooks/use-toast';
import { getCommunities, type Community, type CommunitiesResponse } from '@/lib/api/content';
import { Search, Building2, Folder, Users } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

interface CommunitiesTableProps {
  initialData: CommunitiesResponse;
}

export function CommunitiesTable({ initialData }: CommunitiesTableProps) {
  const { toast } = useToast();
  const [communities, setCommunities] = useState<Community[]>(initialData.communities);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const loadCommunities = async () => {
    setIsLoading(true);
    try {
      const data = await getCommunities({ page, search: search || undefined });
      setCommunities(data.communities);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load communities',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadCommunities();
  }, [page, search]);

  const totalPages = Math.ceil(total / 20);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Communities</CardTitle>
        <div className="flex items-center gap-4 mt-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
            <Input
              placeholder="Search communities..."
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
          <div className="text-center py-8 text-gray-500">Loading communities...</div>
        ) : communities.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No communities found</div>
        ) : (
          <div className="space-y-4">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">Name</th>
                    <th className="text-left p-3 font-medium text-gray-700">Slug</th>
                    <th className="text-left p-3 font-medium text-gray-700">Visibility</th>
                    <th className="text-left p-3 font-medium text-gray-700">Hubs</th>
                    <th className="text-left p-3 font-medium text-gray-700">Members</th>
                    <th className="text-left p-3 font-medium text-gray-700">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {communities.map((community) => (
                    <tr key={community.id} className="border-b hover:bg-gray-50">
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          <Building2 className="w-4 h-4 text-blue-600" />
                          <span className="font-medium text-gray-900">{community.name}</span>
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600 font-mono">{community.slug}</td>
                      <td className="p-3">
                        <Badge variant="secondary">{community.visibility}</Badge>
                      </td>
                      <td className="p-3">
                        <div className="flex items-center gap-1 text-sm text-gray-600">
                          <Folder className="w-4 h-4" />
                          {community.hubCount}
                        </div>
                      </td>
                      <td className="p-3">
                        <div className="flex items-center gap-1 text-sm text-gray-600">
                          <Users className="w-4 h-4" />
                          {community.memberCount}
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {formatDistanceToNow(new Date(community.createdAt), { addSuffix: true })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-4">
                <div className="text-sm text-gray-600">
                  Showing {(page - 1) * 20 + 1}-{Math.min(page * 20, total)} of {total} communities
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
