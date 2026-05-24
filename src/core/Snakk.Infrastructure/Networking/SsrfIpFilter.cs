namespace Snakk.Infrastructure.Networking;

using System.Net;
using System.Net.Http;
using System.Net.Sockets;

/// <summary>
/// Shared SSRF guard for outbound HTTP clients that follow user-supplied URLs
/// (link-preview fetcher, webhook delivery, OEmbed, etc.).
///
/// <see cref="IsPrivateOrReserved"/> classifies an IP as private/reserved.
/// <see cref="CreateSafeConnectCallback"/> returns a SocketsHttpHandler
/// ConnectCallback that re-validates the destination at every TCP connect —
/// catching redirects and DNS-rebinding, which a DelegatingHandler check
/// applied to the user-facing request URI does not.
/// </summary>
public static class SsrfIpFilter
{
    /// <summary>
    /// True if <paramref name="ip"/> falls in a range that must not be reached
    /// from a user-supplied URL: loopback, RFC 1918 private space, CGNAT,
    /// link-local (incl. cloud metadata 169.254/16), broadcast/multicast/reserved,
    /// IPv6 ULA / link-local / loopback / DNS64-embedded private IPv4.
    /// IPv4-mapped IPv6 (::ffff:0:0/96) is mapped down so the IPv4 rules apply.
    /// </summary>
    public static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                0 => true,                                              // 0.0.0.0/8 "this network"
                10 => true,                                             // 10.0.0.0/8
                100 when bytes[1] >= 64 && bytes[1] <= 127 => true,    // 100.64.0.0/10 CGNAT
                127 => true,                                            // 127.0.0.0/8 loopback
                169 when bytes[1] == 254 => true,                       // 169.254.0.0/16 link-local + cloud metadata
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,      // 172.16.0.0/12
                192 when bytes[1] == 168 => true,                       // 192.168.0.0/16
                >= 224 => true,                                         // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved (incl. 255.255.255.255 broadcast)
                _ => false
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // DNS64 well-known prefix 64:ff9b::/96 (RFC 6052): last 32 bits embed an IPv4.
            // Classify by the embedded IPv4 so 64:ff9b::169.254.169.254 is recognised.
            if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b
                && bytes[4] == 0 && bytes[5] == 0 && bytes[6] == 0 && bytes[7] == 0
                && bytes[8] == 0 && bytes[9] == 0 && bytes[10] == 0 && bytes[11] == 0)
            {
                var embedded = new IPAddress(new[] { bytes[12], bytes[13], bytes[14], bytes[15] });
                return IsPrivateOrReserved(embedded);
            }

            if (bytes[0] == 0xff)                                       // ff00::/8 multicast
                return true;

            if (ip.Equals(IPAddress.IPv6Any))                           // :: unspecified
                return true;

            return IPAddress.IsLoopback(ip)                             // ::1
                || ip.IsIPv6LinkLocal                                   // fe80::/10
                || ip.IsIPv6SiteLocal                                   // fec0::/10 (deprecated but still routable on legacy gear)
                || (bytes[0] & 0xfe) == 0xfc;                           // fc00::/7 ULA (fc::/8 + fd::/8)
        }

        return false;
    }

    /// <summary>
    /// Returns a <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves
    /// the destination host and refuses to connect if every resolved address is
    /// private/reserved per <see cref="IsPrivateOrReserved"/>. Runs on the
    /// initial connect AND on every redirect hop, closing the redirect-bypass
    /// gap that a request-level DelegatingHandler cannot cover.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> CreateSafeConnectCallback()
        => async (ctx, ct) =>
        {
            var host = ctx.DnsEndPoint.Host;

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, ct);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"SSRF: DNS resolution failed for '{host}'", ex);
            }

            var safe = addresses.FirstOrDefault(a => !IsPrivateOrReserved(a))
                ?? throw new HttpRequestException(
                    $"SSRF: blocked connect to private/reserved address for host '{host}'");

            var socket = new Socket(safe.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(safe, ctx.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
}
