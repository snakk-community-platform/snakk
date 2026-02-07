import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { User, UserStats } from '@/lib/api/users';
import {
  Calendar,
  Clock,
  MessageSquare,
  FileText,
  Heart,
  Award
} from 'lucide-react';

interface UserProfileProps {
  user: User;
  stats: UserStats;
}

export function UserProfile({ user, stats }: UserProfileProps) {
  function formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'active':
        return 'bg-green-100 text-green-800';
      case 'banned':
        return 'bg-red-100 text-red-800';
      case 'inactive':
        return 'bg-gray-100 text-gray-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getRoleColor = (role: string) => {
    switch (role) {
      case 'Admin':
        return 'bg-purple-100 text-purple-800';
      case 'Moderator':
        return 'bg-blue-100 text-blue-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="space-y-6">
      {/* Profile Card */}
      <Card>
        <CardHeader>
          <CardTitle>Profile Information</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="text-sm text-gray-500 mb-1">Display Name</p>
            <p className="font-medium">{user.displayName}</p>
          </div>

          <div>
            <p className="text-sm text-gray-500 mb-1">Email</p>
            <p className="font-medium">{user.email}</p>
          </div>

          <div>
            <p className="text-sm text-gray-500 mb-1">Role</p>
            <Badge className={getRoleColor(user.role)}>{user.role}</Badge>
          </div>

          <div>
            <p className="text-sm text-gray-500 mb-1">Status</p>
            <Badge className={getStatusColor(user.status)}>{user.status}</Badge>
          </div>

          <div className="pt-4 border-t">
            <div className="flex items-center text-sm text-gray-600 mb-2">
              <Calendar className="w-4 h-4 mr-2" />
              <span>Joined {formatDate(user.createdAt)}</span>
            </div>
            <div className="flex items-center text-sm text-gray-600">
              <Clock className="w-4 h-4 mr-2" />
              <span>Last active {formatDate(user.lastActive)}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Stats Card */}
      <Card>
        <CardHeader>
          <CardTitle>Activity Stats</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center text-gray-600">
              <MessageSquare className="w-4 h-4 mr-2" />
              <span className="text-sm">Posts</span>
            </div>
            <span className="font-semibold">{stats.totalPosts}</span>
          </div>

          <div className="flex items-center justify-between">
            <div className="flex items-center text-gray-600">
              <FileText className="w-4 h-4 mr-2" />
              <span className="text-sm">Discussions</span>
            </div>
            <span className="font-semibold">{stats.totalDiscussions}</span>
          </div>

          <div className="flex items-center justify-between">
            <div className="flex items-center text-gray-600">
              <Heart className="w-4 h-4 mr-2" />
              <span className="text-sm">Reactions</span>
            </div>
            <span className="font-semibold">{stats.totalReactions}</span>
          </div>

          <div className="flex items-center justify-between pt-4 border-t">
            <div className="flex items-center text-gray-600">
              <Award className="w-4 h-4 mr-2" />
              <span className="text-sm">Reputation</span>
            </div>
            <span className="font-semibold text-lg">{stats.reputationScore}</span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
