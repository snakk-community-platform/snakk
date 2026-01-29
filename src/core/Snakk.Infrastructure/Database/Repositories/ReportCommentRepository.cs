namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ReportCommentRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ReportCommentDatabaseEntity>(context), IReportCommentRepository
{
    public async Task<ReportCommentDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(rc => rc.AuthorUser)
            .Include(rc => rc.Report)
            .FirstOrDefaultAsync(rc => rc.PublicId == publicId && !rc.IsDeleted);
    }

    public async Task<IEnumerable<ReportCommentDatabaseEntity>> GetCommentsForReportAsync(int reportId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(rc => rc.AuthorUser)
            .Where(rc => rc.ReportId == reportId && !rc.IsDeleted)
            .OrderBy(rc => rc.CreatedAt)
            .ToListAsync();
    }
}
