namespace Snakk.Application.DTOs.Admin;

public class AdminReportDetailDto
{
    public required string PublicId { get; init; }
    public required string ReporterPublicId { get; init; }
    public required string ReporterDisplayName { get; init; }
    public string? ReportedUserPublicId { get; init; }
    public string? ReportedUserDisplayName { get; init; }
    public string? ReportedPostPublicId { get; init; }
    public string? ReportedDiscussionPublicId { get; init; }
    public string? ReasonName { get; init; }
    public string? ReasonDescription { get; init; }
    public string? Details { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolverPublicId { get; init; }
    public string? ResolverDisplayName { get; init; }
    public string? ResolutionNote { get; init; }
}
