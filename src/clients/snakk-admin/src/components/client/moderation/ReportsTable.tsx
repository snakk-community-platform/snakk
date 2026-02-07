'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import {
  getReports,
  resolveReport,
  dismissReport,
  type Report,
  type ReportsResponse,
} from '@/lib/api/moderation';
import { Search, AlertCircle, CheckCircle, XCircle } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';

interface ReportsTableProps {
  initialData: ReportsResponse;
}

export function ReportsTable({ initialData }: ReportsTableProps) {
  const { toast } = useToast();
  const [reports, setReports] = useState<Report[]>(initialData.reports);
  const [total, setTotal] = useState(initialData.total);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<string>('all');
  const [isLoading, setIsLoading] = useState(false);
  const [selectedReport, setSelectedReport] = useState<Report | null>(null);
  const [actionDialog, setActionDialog] = useState<{
    type: 'resolve' | 'dismiss' | null;
    reportId: string | null;
  }>({ type: null, reportId: null });
  const [resolutionNote, setResolutionNote] = useState('');

  const loadReports = async () => {
    setIsLoading(true);
    try {
      const data = await getReports({
        page,
        status: status === 'all' ? undefined : status,
      });
      setReports(data.reports);
      setTotal(data.total);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load reports',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadReports();
  }, [page, status]);

  const handleResolve = async () => {
    if (!actionDialog.reportId) return;

    try {
      await resolveReport(actionDialog.reportId, resolutionNote || undefined);
      toast({
        title: 'Success',
        description: 'Report resolved successfully',
      });
      setActionDialog({ type: null, reportId: null });
      setResolutionNote('');
      loadReports();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to resolve report',
      });
    }
  };

  const handleDismiss = async () => {
    if (!actionDialog.reportId) return;

    try {
      await dismissReport(actionDialog.reportId, resolutionNote || undefined);
      toast({
        title: 'Success',
        description: 'Report dismissed successfully',
      });
      setActionDialog({ type: null, reportId: null });
      setResolutionNote('');
      loadReports();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to dismiss report',
      });
    }
  };

  const totalPages = Math.ceil(total / 20);

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'pending':
        return (
          <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800">
            <AlertCircle className="w-3 h-3" />
            Pending
          </span>
        );
      case 'resolved':
        return (
          <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800">
            <CheckCircle className="w-3 h-3" />
            Resolved
          </span>
        );
      case 'dismissed':
        return (
          <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
            <XCircle className="w-3 h-3" />
            Dismissed
          </span>
        );
      default:
        return <span className="text-xs text-gray-500">{status}</span>;
    }
  };

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>Reports</CardTitle>
          <div className="flex items-center gap-4 mt-4">
            <div className="flex-1">
              <Select value={status} onValueChange={(value) => {
                setStatus(value);
                setPage(1);
              }}>
                <SelectTrigger>
                  <SelectValue placeholder="All statuses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  <SelectItem value="Pending">Pending</SelectItem>
                  <SelectItem value="Resolved">Resolved</SelectItem>
                  <SelectItem value="Dismissed">Dismissed</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="text-center py-8 text-gray-500">Loading reports...</div>
          ) : reports.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No reports found</div>
          ) : (
            <div className="space-y-4">
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="border-b">
                      <th className="text-left p-3 font-medium text-gray-700">Reporter</th>
                      <th className="text-left p-3 font-medium text-gray-700">Reported User</th>
                      <th className="text-left p-3 font-medium text-gray-700">Content Type</th>
                      <th className="text-left p-3 font-medium text-gray-700">Reason</th>
                      <th className="text-left p-3 font-medium text-gray-700">Status</th>
                      <th className="text-left p-3 font-medium text-gray-700">Created</th>
                      <th className="text-left p-3 font-medium text-gray-700">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {reports.map((report) => (
                      <tr key={report.id} className="border-b hover:bg-gray-50">
                        <td className="p-3 text-sm text-gray-900">{report.reporterUsername}</td>
                        <td className="p-3 text-sm text-gray-900">
                          {report.reportedUsername || 'N/A'}
                        </td>
                        <td className="p-3 text-sm text-gray-600">{report.contentType}</td>
                        <td className="p-3 text-sm text-gray-600">{report.reason}</td>
                        <td className="p-3">{getStatusBadge(report.status)}</td>
                        <td className="p-3 text-sm text-gray-600">
                          {formatDistanceToNow(new Date(report.createdAt), { addSuffix: true })}
                        </td>
                        <td className="p-3">
                          {report.status.toLowerCase() === 'pending' ? (
                            <div className="flex gap-2">
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                  setActionDialog({ type: 'resolve', reportId: report.id });
                                }}
                              >
                                Resolve
                              </Button>
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                  setActionDialog({ type: 'dismiss', reportId: report.id });
                                }}
                              >
                                Dismiss
                              </Button>
                            </div>
                          ) : (
                            <span className="text-xs text-gray-500">
                              {report.status === 'Resolved' ? 'Resolved' : 'Dismissed'} by{' '}
                              {report.resolverUsername}
                            </span>
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
                    Showing {(page - 1) * 20 + 1}-{Math.min(page * 20, total)} of {total} reports
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

      <Dialog
        open={actionDialog.type !== null}
        onOpenChange={(open) => {
          if (!open) {
            setActionDialog({ type: null, reportId: null });
            setResolutionNote('');
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {actionDialog.type === 'resolve' ? 'Resolve Report' : 'Dismiss Report'}
            </DialogTitle>
            <DialogDescription>
              {actionDialog.type === 'resolve'
                ? 'Add a note about how this report was resolved.'
                : 'Add a note about why this report was dismissed.'}
            </DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Textarea
              placeholder="Resolution note (optional)"
              value={resolutionNote}
              onChange={(e) => setResolutionNote(e.target.value)}
              rows={4}
            />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setActionDialog({ type: null, reportId: null });
                setResolutionNote('');
              }}
            >
              Cancel
            </Button>
            <Button
              onClick={actionDialog.type === 'resolve' ? handleResolve : handleDismiss}
            >
              {actionDialog.type === 'resolve' ? 'Resolve' : 'Dismiss'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
