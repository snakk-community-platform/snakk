namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReportCommentRepository : IGenericDatabaseRepository<ReportCommentDatabaseEntity>
{
    Task<ReportCommentDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<IEnumerable<ReportCommentDatabaseEntity>> GetCommentsForReportAsync(int reportId);
}
