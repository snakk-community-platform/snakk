import { StatsCardSkeleton } from "@/components/ui/skeletons/card-skeleton";
import { TableSkeleton } from "@/components/ui/skeletons/table-skeleton";
import { Skeleton } from "@/components/ui/skeleton";

export default function DashboardLoading() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <Skeleton className="h-8 w-48 mb-2" />
        <Skeleton className="h-4 w-96" />
      </div>

      {/* Stats Grid */}
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
        {[1, 2, 3, 4].map((i) => (
          <StatsCardSkeleton key={i} />
        ))}
      </div>

      {/* Charts and Tables */}
      <div className="grid gap-6 md:grid-cols-2">
        <div className="space-y-4">
          <Skeleton className="h-6 w-40" />
          <div className="h-80 bg-gray-200 rounded-lg animate-pulse" />
        </div>
        <div className="space-y-4">
          <Skeleton className="h-6 w-40" />
          <TableSkeleton rows={5} columns={3} showHeader={false} />
        </div>
      </div>
    </div>
  );
}
