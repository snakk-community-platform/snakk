import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Users, MessageSquare, Flag, TrendingUp } from 'lucide-react';
import type { DashboardStats } from '@/lib/api/dashboard';

interface StatsCardsProps {
  stats: DashboardStats;
}

export function StatsCards({ stats }: StatsCardsProps) {
  const cards = [
    {
      title: 'Total Users',
      value: stats.totalUsers.toLocaleString(),
      icon: Users,
      trend: `+${stats.userGrowth}%`,
      trendPositive: true,
    },
    {
      title: 'Active Users (24h)',
      value: stats.activeUsers.toLocaleString(),
      icon: TrendingUp,
    },
    {
      title: 'Discussions',
      value: stats.totalDiscussions.toLocaleString(),
      icon: MessageSquare,
    },
    {
      title: 'Pending Reports',
      value: stats.pendingReports.toLocaleString(),
      icon: Flag,
      highlight: stats.pendingReports > 0,
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {cards.map((card) => (
        <Card key={card.title} className={card.highlight ? 'border-red-500' : ''}>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              {card.title}
            </CardTitle>
            <card.icon className="w-4 h-4 text-gray-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{card.value}</div>
            {card.trend && (
              <p className={`text-xs mt-1 ${card.trendPositive ? 'text-green-500' : 'text-red-500'}`}>
                {card.trend}
              </p>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
