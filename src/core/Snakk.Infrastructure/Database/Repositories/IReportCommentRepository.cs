namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReportCommentRepository : IGenericDatabaseRepository<ReportCommentDatabaseEntity>
{
    Task<ReportCommentDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<IEnumerable<ReportCommentDatabaseEntity>> GetCommentsForReportAsync(int reportId, CancellationToken ct = default);
}
