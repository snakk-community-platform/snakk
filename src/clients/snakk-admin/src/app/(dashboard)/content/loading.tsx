import { CardSkeleton } from "@/components/ui/skeletons/card-skeleton";
import { TableSkeleton } from "@/components/ui/skeletons/table-skeleton";

export default function ContentLoading() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <div className="h-8 w-48 bg-gray-200 rounded animate-pulse mb-2" />
        <div className="h-4 w-96 bg-gray-200 rounded animate-pulse" />
      </div>

      {/* Tabs skeleton */}
      <div className="flex gap-4 border-b">
        {[1, 2, 3, 4].map((i) => (
          <div key={i} className="h-10 w-24 bg-gray-200 rounded-t animate-pulse" />
        ))}
      </div>

      {/* Content */}
      <div className="space-y-4">
        <TableSkeleton rows={10} columns={5} showHeader={false} />
      </div>
    </div>
  );
}
