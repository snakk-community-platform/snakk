using Snakk.Sdk;

namespace Snakk.AdminWeb.Services;

public class AdminApiClientService
{
    private readonly SnakkApiClient _client;

    public AdminApiClientService(SnakkApiClient client)
    {
        _client = client;
    }

    public SnakkApiClient Client => _client;
}
