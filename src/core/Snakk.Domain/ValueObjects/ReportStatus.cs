namespace Snakk.Domain.ValueObjects;

public enum ReportStatus
{
    Pending,    // Awaiting moderator review
    Resolved,   // Action taken
    Dismissed   // No action needed
}
