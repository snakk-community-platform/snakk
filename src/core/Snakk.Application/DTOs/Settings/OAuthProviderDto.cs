namespace Snakk.Application.DTOs.Settings;

public class OAuthProviderDto
{
    public required string Provider { get; set; }
    public bool Enabled { get; set; }
    public bool Configured { get; set; } // True if ClientId/ClientSecret exist in config
}

public class OAuthProvidersResponse
{
    public required List<OAuthProviderDto> Providers { get; set; }
}
