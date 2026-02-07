import { apiRequest } from './client';

export interface User {
  id: string;
  displayName: string;
  email: string;
  role: string;
  status: string;
  createdAt: string;
  lastActive: string;
  avatarUrl?: string;
}

export interface UserStats {
  totalPosts: number;
  totalDiscussions: number;
  totalReactions: number;
  reputationScore: number;
}

export interface UserActivity {
  id: string;
  type: 'post_created' | 'discussion_created' | 'reaction_added' | 'comment_added';
  description: string;
  timestamp: string;
  metadata?: Record<string, any>;
}

export interface UsersResponse {
  users: User[];
  total: number;
  page: number;
  pageSize: number;
}

export async function getUsers(params: {
  page: number;
  search?: string;
  role?: string;
  status?: string;
}): Promise<UsersResponse> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: '20',
    ...(params.search ? { search: params.search } : {}),
    ...(params.role ? { role: params.role } : {}),
    ...(params.status ? { status: params.status } : {}),
  });

  return apiRequest(`/admin/users?${queryParams}`);
}

export async function getUser(id: string): Promise<User | null> {
  try {
    return await apiRequest(`/admin/users/${id}`);
  } catch {
    return null;
  }
}

export async function getUserActivity(id: string): Promise<UserActivity[]> {
  return apiRequest(`/admin/users/${id}/activity?limit=20`);
}

export async function getUserStats(id: string): Promise<UserStats> {
  return apiRequest(`/admin/users/${id}/stats`);
}

export async function banUser(id: string, reason: string, duration?: number) {
  return apiRequest(`/admin/users/${id}/ban`, {
    method: 'POST',
    body: JSON.stringify({ reason, duration: duration ? duration : null }),
  });
}

export async function unbanUser(id: string) {
  return apiRequest(`/admin/users/${id}/ban`, {
    method: 'DELETE',
  });
}

export async function updateUserRole(id: string, role: string) {
  return apiRequest(`/admin/users/${id}/role`, {
    method: 'PUT',
    body: JSON.stringify({ role }),
  });
}
