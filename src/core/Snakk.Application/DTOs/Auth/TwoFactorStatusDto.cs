namespace Snakk.Application.DTOs.Auth;

public class TwoFactorStatusDto
{
    public bool IsEnabled { get; set; }
    public bool HasBackupCodes { get; set; }
    public int UsedBackupCodesCount { get; set; }
    public int TotalBackupCodes { get; set; }
}
