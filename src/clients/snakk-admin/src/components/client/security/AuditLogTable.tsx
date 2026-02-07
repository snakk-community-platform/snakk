'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import {
  getAuditLogs,
  type AuditLog,
  type AuditLogsResponse,
} from '@/lib/api/security';
import { Shield, CheckCircle, XCircle, AlertTriangle, Info } from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Input } from '@/components/ui/input';

interface AuditLogTableProps {
  initialData: AuditLogsResponse;
}

export function AuditLogTable({ initialData }: AuditLogTableProps) {
  const { toast } = useToast();
  const [logs, setLogs] = useState<AuditLog[]>(initialData.logs);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [category, setCategory] = useState<string>('all');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const loadLogs = async () => {
    setIsLoading(true);
    try {
      const data = await getAuditLogs({
        page,
        category: category === 'all' ? undefined : category,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
      });
      setLogs(data.logs);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load audit logs',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadLogs();
  }, [page, category, fromDate, toDate]);

  const totalPages = Math.ceil(total / 50);

  const getSeverityIcon = (severity: string) => {
    switch (severity.toLowerCase()) {
      case 'info':
        return <Info className="w-4 h-4 text-blue-600" />;
      case 'warning':
        return <AlertTriangle className="w-4 h-4 text-yellow-600" />;
      case 'error':
      case 'critical':
        return <XCircle className="w-4 h-4 text-red-600" />;
      default:
        return <Info className="w-4 h-4 text-gray-600" />;
    }
  };

  const getStatusIcon = (success: boolean) => {
    return success ? (
      <CheckCircle className="w-4 h-4 text-green-600" />
    ) : (
      <XCircle className="w-4 h-4 text-red-600" />
    );
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Audit Logs</CardTitle>
        <div className="flex items-center gap-4 mt-4 flex-wrap">
          <div className="flex-1 min-w-[200px]">
            <Select value={category} onValueChange={(value) => {
              setCategory(value);
              setPage(1);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="All categories" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All categories</SelectItem>
                <SelectItem value="User">User</SelectItem>
                <SelectItem value="Content">Content</SelectItem>
                <SelectItem value="Moderation">Moderation</SelectItem>
                <SelectItem value="Security">Security</SelectItem>
                <SelectItem value="System">System</SelectItem>
                <SelectItem value="Authentication">Authentication</SelectItem>
                <SelectItem value="Compliance">Compliance</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="flex gap-2">
            <Input
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(1);
              }}
              className="w-40"
              placeholder="From date"
            />
            <Input
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(1);
              }}
              className="w-40"
              placeholder="To date"
            />
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="text-center py-8 text-gray-500">Loading audit logs...</div>
        ) : logs.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No audit logs found</div>
        ) : (
          <div className="space-y-4">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">Status</th>
                    <th className="text-left p-3 font-medium text-gray-700">Severity</th>
                    <th className="text-left p-3 font-medium text-gray-700">Action</th>
                    <th className="text-left p-3 font-medium text-gray-700">Category</th>
                    <th className="text-left p-3 font-medium text-gray-700">Actor</th>
                    <th className="text-left p-3 font-medium text-gray-700">Target</th>
                    <th className="text-left p-3 font-medium text-gray-700">IP Address</th>
                    <th className="text-left p-3 font-medium text-gray-700">Time</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.map((log) => (
                    <tr key={log.id} className="border-b hover:bg-gray-50">
                      <td className="p-3">{getStatusIcon(log.success)}</td>
                      <td className="p-3">{getSeverityIcon(log.severity)}</td>
                      <td className="p-3">
                        <div className="text-sm font-medium text-gray-900">{log.action}</div>
                        {log.reason && (
                          <div className="text-xs text-gray-500 mt-1">{log.reason}</div>
                        )}
                      </td>
                      <td className="p-3">
                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                          {log.category}
                        </span>
                      </td>
                      <td className="p-3 text-sm text-gray-900">{log.actorUsername}</td>
                      <td className="p-3">
                        {log.targetDisplayName ? (
                          <div className="text-sm text-gray-600">
                            <div className="font-medium">{log.targetType}</div>
                            <div className="text-xs text-gray-500">{log.targetDisplayName}</div>
                          </div>
                        ) : (
                          <span className="text-gray-400">-</span>
                        )}
                      </td>
                      <td className="p-3 text-sm text-gray-600 font-mono">
                        {log.ipAddress || '-'}
                      </td>
                      <td className="p-3">
                        <div className="text-sm text-gray-600">
                          {format(new Date(log.createdAt), 'MMM d, HH:mm')}
                        </div>
                        <div className="text-xs text-gray-500">
                          {formatDistanceToNow(new Date(log.createdAt), { addSuffix: true })}
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
                  Showing {(page - 1) * 50 + 1}-{Math.min(page * 50, total)} of {total} logs
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
