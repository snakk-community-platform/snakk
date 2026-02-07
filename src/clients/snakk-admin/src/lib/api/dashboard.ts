import { apiRequest } from './client';

export interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  totalDiscussions: number;
  pendingReports: number;
  userGrowth: number;
}

export interface Activity {
  id: string;
  type: 'user_registered' | 'report_created' | 'discussion_created' | 'user_banned';
  message: string;
  timestamp: string;
  userId?: string;
}

export async function getStats(): Promise<DashboardStats> {
  // TODO: Replace with actual API endpoint when available
  // For now, return mock data
  return {
    totalUsers: 1247,
    activeUsers: 342,
    totalDiscussions: 5892,
    pendingReports: 12,
    userGrowth: 15.3,
  };
}

export async function getRecentActivity(): Promise<Activity[]> {
  // TODO: Replace with actual API endpoint when available
  // For now, return mock data
  return [
    {
      id: '1',
      type: 'user_registered',
      message: 'New user john.doe@example.com registered',
      timestamp: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    },
    {
      id: '2',
      type: 'report_created',
      message: 'New report submitted for discussion "Spam post"',
      timestamp: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    },
    {
      id: '3',
      type: 'discussion_created',
      message: 'New discussion created: "Welcome to Snakk"',
      timestamp: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
    },
  ];
}
