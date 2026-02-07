import { Suspense } from 'react';
import { UserTable } from '@/components/client/users/UserTable';
import { getUsers } from '@/lib/api/users';
import { Card, CardContent } from '@/components/ui/card';

interface PageProps {
  searchParams: Promise<{
    page?: string;
    search?: string;
    role?: string;
    status?: string;
  }>;
}

function UserTableSkeleton() {
  return (
    <Card>
      <CardContent className="p-6">
        <div className="animate-pulse space-y-4">
          <div className="h-10 bg-gray-200 rounded w-1/3" />
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="h-16 bg-gray-200 rounded" />
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default async function UsersPage({ searchParams }: PageProps) {
  const params = await searchParams;
  const page = parseInt(params.page || '1');
  const search = params.search || '';
  const role = params.role || '';
  const status = params.status || '';

  // Server-side data fetching
  const data = await getUsers({
    page,
    search,
    role,
    status,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Users</h1>
          <p className="text-gray-500 mt-1">Manage and monitor user accounts</p>
        </div>
      </div>

      <Suspense fallback={<UserTableSkeleton />}>
        <UserTable initialData={data} />
      </Suspense>
    </div>
  );
}
