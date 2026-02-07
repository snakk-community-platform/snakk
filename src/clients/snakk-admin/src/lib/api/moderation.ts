import { apiRequest } from './client';

// ==================== Types ====================

export interface Report {
  id: string;
  reporterUsername: string;
  reportedUsername?: string;
  contentType: string;
  reason: string;
  status: string;
  createdAt: string;
  resolvedAt?: string;
  resolverUsername?: string;
  details?: string;
}

export interface ReportDetails {
  id: string;
  reporterId: string;
  reporterUsername: string;
  reportedUserId?: string;
  reportedUsername?: string;
  postId?: string;
  discussionId?: string;
  reason: string;
  reasonDescription: string;
  details?: string;
  status: string;
  createdAt: string;
  resolvedAt?: string;
  resolverId?: string;
  resolverUsername?: string;
  resolutionNote?: string;
}

export interface ReportsResponse {
  reports: Report[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ModerationAction {
  id: string;
  actionType: string;
  moderatorUsername: string;
  targetType: string;
  targetId: string;
  reason?: string;
  details?: string;
  createdAt: string;
  communityName?: string;
  hubName?: string;
  spaceName?: string;
}

export interface ModerationLogResponse {
  actions: ModerationAction[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Ban {
  id: string;
  userId: string;
  username: string;
  banType: string;
  reason: string;
  bannedAt: string;
  expiresAt?: string;
  bannedByUsername: string;
  scope: string;
}

export interface BansResponse {
  bans: Ban[];
  total: number;
  page: number;
  pageSize: number;
}

// ==================== API Functions ====================

export async function getReports(params: {
  page?: number;
  status?: string;
}): Promise<ReportsResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.append('page', params.page.toString());
  if (params.status) searchParams.append('status', params.status);

  const query = searchParams.toString();
  return apiRequest(`/admin/moderation/reports${query ? `?${query}` : ''}`);
}

export async function getReport(id: string): Promise<ReportDetails> {
  return apiRequest(`/admin/moderation/reports/${id}`);
}

export async function resolveReport(id: string, resolutionNote?: string) {
  return apiRequest(`/admin/moderation/reports/${id}/resolve`, {
    method: 'POST',
    body: JSON.stringify({ resolutionNote }),
  });
}

export async function dismissReport(id: string, resolutionNote?: string) {
  return apiRequest(`/admin/moderation/reports/${id}/dismiss`, {
    method: 'POST',
    body: JSON.stringify({ resolutionNote }),
  });
}

export async function getModerationLog(params: {
  page?: number;
  actionType?: string;
}): Promise<ModerationLogResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.append('page', params.page.toString());
  if (params.actionType) searchParams.append('actionType', params.actionType);

  const query = searchParams.toString();
  return apiRequest(`/admin/moderation/log${query ? `?${query}` : ''}`);
}

export async function getActiveBans(params: {
  page?: number;
}): Promise<BansResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.append('page', params.page.toString());

  const query = searchParams.toString();
  return apiRequest(`/admin/moderation/bans${query ? `?${query}` : ''}`);
}
