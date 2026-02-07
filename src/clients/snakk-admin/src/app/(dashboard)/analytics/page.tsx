import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export default function AnalyticsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Analytics</h1>
        <p className="text-gray-500 mt-1">Platform analytics and insights</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Coming in Phase 6</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-gray-600 mb-4">
            Analytics features will include:
          </p>
          <ul className="list-disc list-inside space-y-2 text-gray-600">
            <li>User growth charts (daily/weekly/monthly)</li>
            <li>Engagement metrics (posts, reactions, active users)</li>
            <li>Content performance analytics</li>
            <li>Custom date range reports</li>
            <li>Export functionality (CSV/PDF)</li>
            <li>Real-time metrics dashboard</li>
          </ul>
        </CardContent>
      </Card>
    </div>
  );
}
