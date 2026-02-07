'use client'

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import {
  getFailedLogins,
  getActiveSessions,
  getSuspiciousActivities,
  type FailedLoginsResponse,
  type ActiveSessionsResponse,
  type SuspiciousActivitiesResponse,
} from '@/lib/api/security';
import { AlertTriangle, Shield, Activity, Clock } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

interface SecurityMonitoringProps {
  initialFailedLogins: FailedLoginsResponse;
  initialSessions: ActiveSessionsResponse;
  initialActivities: SuspiciousActivitiesResponse;
}

export function SecurityMonitoring({
  initialFailedLogins,
  initialSessions,
  initialActivities,
}: SecurityMonitoringProps) {
  const { toast } = useToast();
  const [failedLogins, setFailedLogins] = useState(initialFailedLogins);
  const [sessions, setSessions] = useState(initialSessions);
  const [activities, setActivities] = useState(initialActivities);
  const [isLoadingLogins, setIsLoadingLogins] = useState(false);
  const [isLoadingSessions, setIsLoadingSessions] = useState(false);
  const [isLoadingActivities, setIsLoadingActivities] = useState(false);

  const refreshSessions = async () => {
    setIsLoadingSessions(true);
    try {
      const data = await getActiveSessions();
      setSessions(data);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to load active sessions',
      });
    } finally {
      setIsLoadingSessions(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Suspicious IPs */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <AlertTriangle className="w-5 h-5 text-red-600" />
              <CardTitle>Suspicious IP Addresses (24h)</CardTitle>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {failedLogins.suspiciousIps.length === 0 ? (
            <div className="text-center py-4 text-gray-500">No suspicious activity detected</div>
          ) : (
            <div className="space-y-2">
              {failedLogins.suspiciousIps.map((ip) => (
                <div
                  key={ip.ipAddress}
                  className="flex items-center justify-between p-3 border rounded-lg hover:bg-gray-50"
                >
                  <div>
                    <div className="font-mono text-sm font-medium">{ip.ipAddress}</div>
                    <div className="text-xs text-gray-500">
                      Last attempt: {formatDistanceToNow(new Date(ip.lastAttempt), { addSuffix: true })}
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="text-lg font-bold text-red-600">{ip.failureCount}</div>
                    <div className="text-xs text-gray-500">failed attempts</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Active Sessions */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Activity className="w-5 h-5 text-green-600" />
              <CardTitle>Active Sessions ({sessions.total})</CardTitle>
            </div>
            <Button variant="outline" size="sm" onClick={refreshSessions} disabled={isLoadingSessions}>
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {isLoadingSessions ? (
            <div className="text-center py-4 text-gray-500">Loading sessions...</div>
          ) : sessions.sessions.length === 0 ? (
            <div className="text-center py-4 text-gray-500">No active sessions</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3 font-medium text-gray-700">User</th>
                    <th className="text-left p-3 font-medium text-gray-700">Email</th>
                    <th className="text-left p-3 font-medium text-gray-700">Started</th>
                    <th className="text-left p-3 font-medium text-gray-700">Last Used</th>
                    <th className="text-left p-3 font-medium text-gray-700">Expires</th>
                  </tr>
                </thead>
                <tbody>
                  {sessions.sessions.slice(0, 10).map((session) => (
                    <tr key={session.userId + session.createdAt} className="border-b hover:bg-gray-50">
                      <td className="p-3 text-sm font-medium text-gray-900">{session.username}</td>
                      <td className="p-3 text-sm text-gray-600">{session.email}</td>
                      <td className="p-3 text-sm text-gray-600">
                        {formatDistanceToNow(new Date(session.createdAt), { addSuffix: true })}
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {session.lastUsedAt
                          ? formatDistanceToNow(new Date(session.lastUsedAt), { addSuffix: true })
                          : 'Never'}
                      </td>
                      <td className="p-3 text-sm text-gray-600">
                        {formatDistanceToNow(new Date(session.expiresAt), { addSuffix: true })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {sessions.total > 10 && (
                <div className="text-center py-2 text-sm text-gray-500">
                  Showing 10 of {sessions.total} active sessions
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Suspicious Activities */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Shield className="w-5 h-5 text-orange-600" />
            <CardTitle>Suspicious Activities (24h)</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          {activities.activities.length === 0 ? (
            <div className="text-center py-4 text-gray-500">No suspicious activities detected</div>
          ) : (
            <div className="space-y-2">
              {activities.activities.map((activity) => (
                <div
                  key={activity.id}
                  className="flex items-center justify-between p-3 border rounded-lg hover:bg-gray-50"
                >
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-gray-900">{activity.action}</span>
                      <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-orange-100 text-orange-800">
                        {activity.severity}
                      </span>
                    </div>
                    <div className="text-xs text-gray-500 mt-1">
                      {activity.actorUsername} • {activity.category}
                      {activity.targetDisplayName && ` • ${activity.targetDisplayName}`}
                    </div>
                  </div>
                  <div className="text-right text-xs text-gray-500">
                    {formatDistanceToNow(new Date(activity.createdAt), { addSuffix: true })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
