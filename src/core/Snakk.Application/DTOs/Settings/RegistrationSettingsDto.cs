namespace Snakk.Application.DTOs.Settings;

public class RegistrationSettingsDto
{
    public string Mode { get; set; } = "Open"; // "Open" | "InviteOnly" | "Closed"
    public string InviteCode { get; set; } = "";
}
