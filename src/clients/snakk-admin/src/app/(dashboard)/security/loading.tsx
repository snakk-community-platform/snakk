import { CardSkeleton } from "@/components/ui/skeletons/card-skeleton";
import { TableSkeleton } from "@/components/ui/skeletons/table-skeleton";

export default function SecurityLoading() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <div className="h-8 w-64 bg-gray-200 rounded animate-pulse mb-2" />
        <div className="h-4 w-96 bg-gray-200 rounded animate-pulse" />
      </div>

      {/* Tabs skeleton */}
      <div className="flex gap-4 border-b">
        {[1, 2, 3, 4].map((i) => (
          <div key={i} className="h-10 w-32 bg-gray-200 rounded-t animate-pulse" />
        ))}
      </div>

      {/* Content - multiple cards/tables */}
      <div className="grid gap-6">
        <TableSkeleton rows={8} columns={6} />
        <div className="grid grid-cols-2 gap-6">
          <CardSkeleton />
          <CardSkeleton />
        </div>
      </div>
    </div>
  );
}
