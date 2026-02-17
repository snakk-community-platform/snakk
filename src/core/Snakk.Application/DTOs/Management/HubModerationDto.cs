namespace Snakk.Application.DTOs.Management;

public class HubModerationDto
{
    public List<ModerationReportDto> PendingReports { get; set; } = new();
    public List<ModerationActionDto> RecentActions { get; set; } = new();
    public ModerationStatsDto Stats { get; set; } = new();
}
