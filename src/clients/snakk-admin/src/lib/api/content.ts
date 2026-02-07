import { apiRequest } from './client';

export interface ContentOverview {
  totalCommunities: number;
  totalHubs: number;
  totalSpaces: number;
  totalDiscussions: number;
  totalPosts: number;
  pinnedDiscussions: number;
  lockedDiscussions: number;
}

export interface Community {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  visibility: string;
  createdAt: string;
  hubCount: number;
  memberCount: number;
}

export interface CommunityDetail extends Community {
  hubs: {
    id: string;
    name: string;
    slug: string;
    spaceCount: number;
  }[];
}

export interface Hub {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  communityId: string;
  communityName: string;
  createdAt: string;
  spaceCount: number;
}

export interface Space {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  hubId: string;
  hubName: string;
  communityId: string;
  communityName: string;
  createdAt: string;
  discussionCount: number;
}

export interface Discussion {
  id: string;
  title: string;
  slug: string;
  spaceId: string;
  spaceName: string;
  hubId: string;
  hubName: string;
  communityId: string;
  communityName: string;
  authorId: string;
  authorName: string;
  postCount: number;
  isPinned: boolean;
  isLocked: boolean;
  createdAt: string;
  lastActivityAt: string | null;
}

export interface DiscussionDetail extends Discussion {
  reactionCount: number;
  tags: string | null;
}

export interface CommunitiesResponse {
  communities: Community[];
  total: number;
  page: number;
  pageSize: number;
}

export interface HubsResponse {
  hubs: Hub[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SpacesResponse {
  spaces: Space[];
  total: number;
  page: number;
  pageSize: number;
}

export interface DiscussionsResponse {
  discussions: Discussion[];
  total: number;
  page: number;
  pageSize: number;
}

// Overview
export async function getContentOverview(): Promise<ContentOverview> {
  return apiRequest('/admin/content/overview');
}

// Communities
export async function getCommunities(params: {
  page: number;
  search?: string;
}): Promise<CommunitiesResponse> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: '20',
    ...(params.search ? { search: params.search } : {}),
  });

  return apiRequest(`/admin/content/communities?${queryParams}`);
}

export async function getCommunity(id: string): Promise<CommunityDetail | null> {
  try {
    return await apiRequest(`/admin/content/communities/${id}`);
  } catch {
    return null;
  }
}

// Hubs
export async function getHubs(params: {
  page: number;
  search?: string;
  communityId?: string;
}): Promise<HubsResponse> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: '20',
    ...(params.search ? { search: params.search } : {}),
    ...(params.communityId ? { communityId: params.communityId } : {}),
  });

  return apiRequest(`/admin/content/hubs?${queryParams}`);
}

// Spaces
export async function getSpaces(params: {
  page: number;
  search?: string;
  hubId?: string;
}): Promise<SpacesResponse> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: '20',
    ...(params.search ? { search: params.search } : {}),
    ...(params.hubId ? { hubId: params.hubId } : {}),
  });

  return apiRequest(`/admin/content/spaces?${queryParams}`);
}

// Discussions
export async function getDiscussions(params: {
  page: number;
  search?: string;
  spaceId?: string;
  isPinned?: boolean;
  isLocked?: boolean;
}): Promise<DiscussionsResponse> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: '20',
    ...(params.search ? { search: params.search } : {}),
    ...(params.spaceId ? { spaceId: params.spaceId } : {}),
    ...(params.isPinned !== undefined ? { isPinned: params.isPinned.toString() } : {}),
    ...(params.isLocked !== undefined ? { isLocked: params.isLocked.toString() } : {}),
  });

  return apiRequest(`/admin/content/discussions?${queryParams}`);
}

export async function getDiscussion(id: string): Promise<DiscussionDetail | null> {
  try {
    return await apiRequest(`/admin/content/discussions/${id}`);
  } catch {
    return null;
  }
}

// Discussion actions
export async function pinDiscussion(id: string) {
  return apiRequest(`/admin/content/discussions/${id}/pin`, {
    method: 'POST',
  });
}

export async function unpinDiscussion(id: string) {
  return apiRequest(`/admin/content/discussions/${id}/pin`, {
    method: 'DELETE',
  });
}

export async function lockDiscussion(id: string) {
  return apiRequest(`/admin/content/discussions/${id}/lock`, {
    method: 'POST',
  });
}

export async function unlockDiscussion(id: string) {
  return apiRequest(`/admin/content/discussions/${id}/lock`, {
    method: 'DELETE',
  });
}

export async function deleteDiscussion(id: string) {
  return apiRequest(`/admin/content/discussions/${id}`, {
    method: 'DELETE',
  });
}
