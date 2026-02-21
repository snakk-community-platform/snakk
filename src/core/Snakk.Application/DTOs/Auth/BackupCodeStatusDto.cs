namespace Snakk.Application.DTOs.Auth;

public class BackupCodeStatusDto
{
    public required List<string> Codes { get; set; }
    public int UsedCount { get; set; }
    public int TotalCount { get; set; }
}
