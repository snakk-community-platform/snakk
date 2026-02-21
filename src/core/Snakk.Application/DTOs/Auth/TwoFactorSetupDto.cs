namespace Snakk.Application.DTOs.Auth;

public class TwoFactorSetupDto
{
    public required string Secret { get; set; }
    public required string QrCodeUrl { get; set; }
}
