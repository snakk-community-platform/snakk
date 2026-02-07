'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import {
  getModerationLog,
  type ModerationAction,
  type ModerationLogResponse,
} from '@/lib/api/moderation';
import { Shield, Ban, UserX, Lock, Unlock, Trash2, Pin, PinOff } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

interface ModerationLogTableProps {
  initialData: ModerationLogResponse;
}

export function ModerationLogTable({ initialData }: ModerationLogTableProps) {
  const { toast } = useToast();
  const [actions, setActions] = useState<ModerationAction[]>(initialData.actions);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);

  const loadLog = async () => {
    setIsLoading(true);
    try {
      const data = await getModerationLog({ page });
      setActions(data.actions);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load moderation log',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadLog();
  }, [page]);

  const totalPages = Math.ceil(total / 20);

  const getActionIcon = (actionType: string) => {
    const type = actionType.toLowerCase();
    if (type.includes('ban')) return <Ban className="w-4 h-4 text-red-600" />;
    if (type.includes('unban')) return <UserX className="w-4 h-4 text-green-600" />;
    if (type.includes('lock')) return <Lock className="w-4 h-4 text-orange-600" />;
    if (type.includes('unlock')) return <Unlock className="w-4 h-4 text-green-600" />;
    if (type.includes('delete')) return <Trash2 className="w-4 h-4 text-red-600" />;
    if (type.includes('pin') && !type.includes('unpin')) return <Pin className="w-4 h-4 text-blue-600" />;
    if (type.includes('unpin')) return <PinOff className="w-4 h-4 text-gray-600" />;
    return <Shield className="w-4 h-4 text-purple-600" />;
  };

  const formatActionType = (actionType: string) => {
    // Convert PascalCase/camelCase to readable format
    return actionType
      .replace(/([A-Z])/g, ' $1')
      .trim()
      .replace(/^./, (str) => str.toUpperCase());
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Moderation Log</CardTitle>
        <p className="text-sm text-gray-500 mt-1">
          All moderation actions taken across the platform
        </p>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="text-center py-8 text-gray-500">Loading moderation log...</div>
        ) : actions.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No moderation actions found</div>
        ) : (
          <div className="space-y-4">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">Action</th>
                    <th className="text-left p-3 font-medium text-gray-700">Moderator</th>
                    <th className="text-left p-3 font-medium text-gray-700">Target</th>
                    <th className="text-left p-3 font-medium text-gray-700">Scope</th>
                    <th className="text-left p-3 font-medium text-gray-700">Reason</th>
                    <th className="text-left p-3 font-medium text-gray-700">Time</th>
                  </tr>
                </thead>
                <tbody>
                  {actions.map((action) => (
                    <tr key={action.id} className="border-b hover:bg-gray-50">
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          {getActionIcon(action.actionType)}
                          <span className="text-sm font-medium text-gray-900">
                            {formatActionType(action.actionType)}
                          </span>
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-900">{action.moderatorUsername}</td>
                      <td className="p-3">
                        <div className="text-sm text-gray-600">
                          <div className="font-medium">{action.targetType}</div>
                          <div className="text-xs text-gray-500 font-mono">{action.targetId}</div>
                        </div>
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {action.communityName && (
                          <div className="text-xs">
                            {action.communityName}
                            {action.hubName && ` / ${action.hubName}`}
                            {action.spaceName && ` / ${action.spaceName}`}
                          </div>
                        )}
                        {!action.communityName && (
                          <span className="text-xs text-gray-400">Platform-wide</span>
                        )}
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {action.reason ? (
                          <div className="max-w-xs truncate" title={action.reason}>
                            {action.reason}
                          </div>
                        ) : (
                          <span className="text-gray-400">-</span>
                        )}
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {formatDistanceToNow(new Date(action.createdAt), { addSuffix: true })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-4">
                <div className="text-sm text-gray-600">
                  Showing {(page - 1) * 20 + 1}-{Math.min(page * 20, total)} of {total} actions
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
