namespace Snakk.Api.Middleware;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// Rejects outbound HTTP requests whose hostname resolves to a private or
/// link-local address, blocking SSRF via user-submitted URLs.
/// Applied only to the "LinkMetadata" named client (user-supplied URLs).
/// The "LinkMetadataProxy" client is intentionally unblocked so it can
/// reach internal Docker services configured as a fetch proxy.
/// </summary>
public class PrivateIpBlockHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host
            ?? throw new InvalidOperationException("Request URI missing host");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"SSRF: DNS resolution failed for '{host}'", ex);
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivateOrReserved))
            throw new HttpRequestException(
                $"SSRF: blocked request to private/reserved address for host '{host}'");

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsPrivateOrReserved(IPAddress addr)
    {
        if (IPAddress.IsLoopback(addr)) return true;

        if (addr.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = addr.GetAddressBytes();
            return b[0] == 10                                    // 10.0.0.0/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12 (Docker bridge et al.)
                || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 (cloud metadata)
                || b[0] == 127;                                  // 127.0.0.0/8
        }

        if (addr.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return addr.IsIPv6LinkLocal      // fe80::/10
                || addr.IsIPv6UniqueLocal    // fc00::/7
                || addr.Equals(IPAddress.IPv6Loopback);  // ::1
        }

        return false;
    }
}
