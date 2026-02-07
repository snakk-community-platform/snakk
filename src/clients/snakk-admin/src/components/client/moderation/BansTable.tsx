'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import { getActiveBans, type Ban, type BansResponse } from '@/lib/api/moderation';
import { Ban as BanIcon, Clock, Globe } from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';

interface BansTableProps {
  initialData: BansResponse;
}

export function BansTable({ initialData }: BansTableProps) {
  const { toast } = useToast();
  const [bans, setBans] = useState<Ban[]>(initialData.bans);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);

  const loadBans = async () => {
    setIsLoading(true);
    try {
      const data = await getActiveBans({ page });
      setBans(data.bans);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load bans',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadBans();
  }, [page]);

  const totalPages = Math.ceil(total / 20);

  const getBanTypeBadge = (banType: string) => {
    const type = banType.toLowerCase();
    if (type.includes('readwrite') || type === 'full') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800">
          Full Ban
        </span>
      );
    }
    if (type.includes('write')) {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-orange-100 text-orange-800">
          Write Ban
        </span>
      );
    }
    if (type.includes('read')) {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800">
          Read Ban
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
        {banType}
      </span>
    );
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Active Bans</CardTitle>
        <p className="text-sm text-gray-500 mt-1">
          All currently active bans across the platform
        </p>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="text-center py-8 text-gray-500">Loading bans...</div>
        ) : bans.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No active bans found</div>
        ) : (
          <div className="space-y-4">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">User</th>
                    <th className="text-left p-3 font-medium text-gray-700">Ban Type</th>
                    <th className="text-left p-3 font-medium text-gray-700">Scope</th>
                    <th className="text-left p-3 font-medium text-gray-700">Reason</th>
                    <th className="text-left p-3 font-medium text-gray-700">Banned By</th>
                    <th className="text-left p-3 font-medium text-gray-700">Banned At</th>
                    <th className="text-left p-3 font-medium text-gray-700">Expires</th>
                  </tr>
                </thead>
                <tbody>
                  {bans.map((ban) => (
                    <tr key={ban.id} className="border-b hover:bg-gray-50">
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          <BanIcon className="w-4 h-4 text-red-600" />
                          <div>
                            <div className="text-sm font-medium text-gray-900">{ban.username}</div>
                            <div className="text-xs text-gray-500 font-mono">{ban.userId}</div>
                          </div>
                        </div>
                      </td>
                      <td className="p-3">{getBanTypeBadge(ban.banType)}</td>
                      <td className="p-3">
                        <div className="flex items-center gap-1 text-sm text-gray-600">
                          {ban.scope.includes('Platform-wide') && (
                            <Globe className="w-4 h-4 text-blue-600" />
                          )}
                          {ban.scope}
                        </div>
                      </td>
                      <td className="p-3">
                        <div className="max-w-xs text-sm text-gray-600 truncate" title={ban.reason}>
                          {ban.reason}
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">{ban.bannedByUsername}</td>
                      <td className="p-3 text-sm text-gray-600">
                        <div>{format(new Date(ban.bannedAt), 'MMM d, yyyy')}</div>
                        <div className="text-xs text-gray-500">
                          {formatDistanceToNow(new Date(ban.bannedAt), { addSuffix: true })}
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {ban.expiresAt ? (
                          <div className="flex items-center gap-1">
                            <Clock className="w-3 h-3 text-orange-600" />
                            <div>
                              <div>{format(new Date(ban.expiresAt), 'MMM d, yyyy')}</div>
                              <div className="text-xs text-gray-500">
                                {formatDistanceToNow(new Date(ban.expiresAt), { addSuffix: true })}
                              </div>
                            </div>
                          </div>
                        ) : (
                          <span className="text-gray-400 italic">Permanent</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-4">
                <div className="text-sm text-gray-600">
                  Showing {(page - 1) * 20 + 1}-{Math.min(page * 20, total)} of {total} bans
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
