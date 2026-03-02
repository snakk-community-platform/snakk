using Snakk.Sdk;

namespace Snakk.AdminWeb.Services;

public class AdminApiClientService(SnakkApiClient client)
{
    public SnakkApiClient Client => client;
}
