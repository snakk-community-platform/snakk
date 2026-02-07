'use client'

import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/hooks/use-toast';
import { exportUserData } from '@/lib/api/security';
import { Download, FileText, Shield } from 'lucide-react';

export function ComplianceTools() {
  const { toast } = useToast();
  const [userId, setUserId] = useState('');
  const [isExporting, setIsExporting] = useState(false);

  const handleExport = async () => {
    if (!userId.trim()) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Please enter a user ID',
      });
      return;
    }

    setIsExporting(true);
    try {
      const result = await exportUserData(userId);
      toast({
        title: 'Export Queued',
        description: result.message,
      });
      setUserId('');
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to queue user data export',
      });
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* GDPR Data Export */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Download className="w-5 h-5 text-blue-600" />
            <CardTitle>GDPR Data Export</CardTitle>
          </div>
          <p className="text-sm text-gray-500 mt-2">
            Export all data associated with a user account for GDPR compliance
          </p>
        </CardHeader>
        <CardContent>
          <div className="flex gap-2">
            <Input
              placeholder="Enter user ID"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              className="flex-1"
            />
            <Button onClick={handleExport} disabled={isExporting}>
              {isExporting ? 'Exporting...' : 'Export Data'}
            </Button>
          </div>
          <div className="mt-4 p-4 bg-blue-50 border border-blue-200 rounded-lg">
            <div className="flex items-start gap-2">
              <FileText className="w-4 h-4 text-blue-600 mt-0.5" />
              <div className="text-sm text-blue-800">
                <p className="font-medium">What data is included:</p>
                <ul className="list-disc list-inside mt-2 space-y-1">
                  <li>User profile information</li>
                  <li>All posts and discussions</li>
                  <li>Reactions and follows</li>
                  <li>Account activity history</li>
                  <li>Moderation history</li>
                </ul>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Privacy & Compliance Info */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Shield className="w-5 h-5 text-green-600" />
            <CardTitle>Privacy & Compliance</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="p-4 border rounded-lg">
              <h3 className="font-medium text-gray-900 mb-2">Data Retention</h3>
              <p className="text-sm text-gray-600">
                User data is retained for the lifetime of the account. Deleted accounts are soft-deleted
                and can be recovered within 30 days.
              </p>
            </div>
            <div className="p-4 border rounded-lg">
              <h3 className="font-medium text-gray-900 mb-2">Right to be Forgotten</h3>
              <p className="text-sm text-gray-600">
                Users can request complete account deletion. This will permanently remove all personal
                data after the 30-day grace period.
              </p>
            </div>
            <div className="p-4 border rounded-lg">
              <h3 className="font-medium text-gray-900 mb-2">Data Processing</h3>
              <p className="text-sm text-gray-600">
                All user data is processed in accordance with GDPR and other applicable privacy laws.
                Audit logs track all data access and modifications.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
