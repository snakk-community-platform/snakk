import { TableSkeleton } from "@/components/ui/skeletons/table-skeleton";
import { Skeleton } from "@/components/ui/skeleton";

export default function ModerationLoading() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <Skeleton className="h-8 w-48 mb-2" />
        <Skeleton className="h-4 w-96" />
      </div>

      {/* Tabs */}
      <div className="flex gap-4 border-b">
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-10 w-24 rounded-t" />
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-4">
        <Skeleton className="h-10 w-40" />
        <Skeleton className="h-10 w-40" />
        <Skeleton className="h-10 w-40" />
      </div>

      {/* Content */}
      <TableSkeleton rows={10} columns={6} showHeader={false} />
    </div>
  );
}
