import { apiRequest } from './client';

// ==================== Types ====================

export interface AuditLog {
  id: string;
  action: string;
  category: string;
  actorUsername: string;
  actorUserId?: string;
  targetType?: string;
  targetId?: string;
  targetDisplayName?: string;
  details?: string;
  reason?: string;
  ipAddress?: string;
  success: boolean;
  errorMessage?: string;
  severity: string;
  createdAt: string;
}

export interface AuditLogDetails extends AuditLog {
  userAgent?: string;
}

export interface AuditLogsResponse {
  logs: AuditLog[];
  total: number;
  page: number;
  pageSize: number;
}

export interface FailedLogin {
  id: string;
  ipAddress: string;
  userAgent?: string;
  details?: string;
  errorMessage?: string;
  createdAt: string;
}

export interface SuspiciousIp {
  ipAddress: string;
  failureCount: number;
  lastAttempt: string;
}

export interface FailedLoginsResponse {
  failures: FailedLogin[];
  suspiciousIps: SuspiciousIp[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ActiveSession {
  userId: string;
  username: string;
  email: string;
  createdAt: string;
  expiresAt: string;
  lastUsedAt?: string;
}

export interface ActiveSessionsResponse {
  sessions: ActiveSession[];
  total: number;
}

export interface SuspiciousActivity {
  id: string;
  action: string;
  category: string;
  actorUsername: string;
  targetType?: string;
  targetDisplayName?: string;
  ipAddress?: string;
  severity: string;
  success: boolean;
  createdAt: string;
}

export interface SuspiciousActivitiesResponse {
  activities: SuspiciousActivity[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UserDataExportResponse {
  exportId: string;
  userId: string;
  status: string;
  message: string;
}

// ==================== API Functions ====================

// Audit Logs
export async function getAuditLogs(params: {
  page?: number;
  category?: string;
  action?: string;
  actorUserId?: string;
  fromDate?: string;
  toDate?: string;
}): Promise<AuditLogsResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.append('page', params.page.toString());
  if (params.category) searchParams.append('category', params.category);
  if (params.action) searchParams.append('action', params.action);
  if (params.actorUserId) searchParams.append('actorUserId', params.actorUserId);
  if (params.fromDate) searchParams.append('fromDate', params.fromDate);
  if (params.toDate) searchParams.append('toDate', params.toDate);

  const query = searchParams.toString();
  return apiRequest(`/admin/security/audit-logs${query ? `?${query}` : ''}`);
}

export async function getAuditLog(id: string): Promise<AuditLogDetails> {
  return apiRequest(`/admin/security/audit-logs/${id}`);
}

// Security Monitoring
export async function getFailedLogins(params: {
  page?: number;
  fromDate?: string;
  toDate?: string;
}): Promise<FailedLoginsResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.append('page', params.page.toString());
  if (params.fromDate) searchParams.append('fromDate', params.fromDate);
  if (params.toDate) searchParams.append('toDate', params.toDate);

  const query = searchParams.toString();
  return apiRequest(`/admin/security/failed-logins${query ? `?${query}` : ''}`);
}

export async function getActiveSessions(): Promise<ActiveSessionsResponse> {
  return apiRequest('/admin/security/active-sessions');
}

export async function getSuspiciousActivities(params: {
  page?: number;
}): Promise<SuspiciousActivitiesResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.append('page', params.page.toString());

  const query = searchParams.toString();
  return apiRequest(`/admin/security/suspicious-activities${query ? `?${query}` : ''}`);
}

// Compliance Tools
export async function exportUserData(userId: string): Promise<UserDataExportResponse> {
  return apiRequest(`/admin/security/user-data-export/${userId}`, {
    method: 'POST',
  });
}

export async function getUserDataExport(exportId: string): Promise<UserDataExportResponse> {
  return apiRequest(`/admin/security/user-data-export/${exportId}`);
}
