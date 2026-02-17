namespace Snakk.Application.DTOs.Settings;

public class SiteInfoDto
{
    public string SiteName { get; set; } = "Snakk";
    public string SiteDescription { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string Language { get; set; } = "en";
}
