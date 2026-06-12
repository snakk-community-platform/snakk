namespace Snakk.Application.Repositories;

/// <summary>
/// Repository for reading and writing pre-computed global stats rollup rows.
/// </summary>
public interface IStatsRollupRepository
{
    /// <summary>
    /// Reads all rows for the given stat kind, ordered by Rank ascending.
    /// Returns an empty list if no rows exist yet.
    /// </summary>
    Task<List<StatsRollupRow>> GetRowsAsync(string statKind, CancellationToken ct = default);

    /// <summary>
    /// Replaces all rows for a stat kind atomically (delete existing + insert new in one transaction).
    /// </summary>
    Task ReplaceKindAsync(string statKind, IReadOnlyList<StatsRollupRow> rows, CancellationToken ct = default);
}

/// <summary>
/// A single pre-computed row from the rollup table.
/// </summary>
public record StatsRollupRow(
    string StatKind,
    int Rank,
    string PayloadJson,
    DateTime ComputedAt);
