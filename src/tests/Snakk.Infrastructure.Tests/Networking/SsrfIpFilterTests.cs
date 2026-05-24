using System.Net;
using Snakk.Infrastructure.Networking;

namespace Snakk.Infrastructure.Tests.Networking;

public class SsrfIpFilterTests
{
    [Test]
    [Arguments("127.0.0.1")]            // loopback
    [Arguments("127.0.0.53")]            // resolver loopback
    [Arguments("10.0.0.1")]              // RFC 1918
    [Arguments("10.255.255.255")]        // RFC 1918 upper
    [Arguments("172.16.0.1")]            // RFC 1918 / Docker bridge
    [Arguments("172.31.255.254")]        // RFC 1918 upper
    [Arguments("192.168.1.1")]           // RFC 1918
    [Arguments("169.254.169.254")]       // AWS/GCP/Azure metadata
    [Arguments("169.254.1.1")]           // link-local
    [Arguments("100.64.0.1")]            // CGNAT (HI-55)
    [Arguments("100.127.255.254")]       // CGNAT upper (HI-55)
    [Arguments("0.0.0.0")]               // "this network" (HI-55)
    [Arguments("0.1.2.3")]               // 0.0.0.0/8 (HI-55)
    [Arguments("224.0.0.1")]             // multicast (HI-55)
    [Arguments("239.255.255.250")]       // SSDP multicast (HI-55)
    [Arguments("255.255.255.255")]       // broadcast (HI-55)
    [Arguments("240.0.0.1")]             // reserved future use (HI-55)
    public async Task IsPrivateOrReserved_BlocksUnsafeIPv4(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsTrue();
    }

    [Test]
    [Arguments("8.8.8.8")]               // public DNS
    [Arguments("1.1.1.1")]               // public DNS
    [Arguments("93.184.215.14")]         // example.com
    [Arguments("9.9.9.9")]               // Quad9
    [Arguments("100.63.255.255")]        // just below CGNAT
    [Arguments("100.128.0.0")]           // just above CGNAT
    [Arguments("172.15.255.255")]        // just below RFC 1918 12-bit
    [Arguments("172.32.0.0")]            // just above RFC 1918 12-bit
    [Arguments("11.0.0.1")]              // just above 10/8
    [Arguments("223.255.255.255")]       // just below multicast
    public async Task IsPrivateOrReserved_AllowsPublicIPv4(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsFalse();
    }

    [Test]
    [Arguments("::1")]                   // IPv6 loopback
    [Arguments("::")]                    // unspecified (HI-55)
    [Arguments("fe80::1")]               // link-local
    [Arguments("fec0::1")]               // deprecated site-local
    [Arguments("fc00::1")]               // ULA
    [Arguments("fd12:3456:789a::1")]     // ULA
    [Arguments("ff02::1")]               // multicast
    [Arguments("ff05::1:3")]             // multicast
    public async Task IsPrivateOrReserved_BlocksUnsafeIPv6(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsTrue();
    }

    [Test]
    [Arguments("2001:4860:4860::8888")]  // Google public DNS
    [Arguments("2606:4700:4700::1111")]  // Cloudflare public DNS
    [Arguments("2001:db8::1")]           // documentation prefix (technically reserved but routable test)
    public async Task IsPrivateOrReserved_AllowsPublicIPv6(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsFalse();
    }

    // HI-55: IPv4-mapped IPv6 must classify by the embedded IPv4.
    [Test]
    [Arguments("::ffff:127.0.0.1")]      // loopback via v4-mapped
    [Arguments("::ffff:10.0.0.1")]       // RFC 1918 via v4-mapped
    [Arguments("::ffff:169.254.169.254")] // cloud metadata via v4-mapped
    [Arguments("::ffff:100.64.0.1")]     // CGNAT via v4-mapped
    public async Task IsPrivateOrReserved_BlocksIPv4MappedIPv6Private(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsTrue();
    }

    [Test]
    [Arguments("::ffff:8.8.8.8")]
    [Arguments("::ffff:1.1.1.1")]
    public async Task IsPrivateOrReserved_AllowsIPv4MappedIPv6Public(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsFalse();
    }

    // HI-55: DNS64 well-known prefix 64:ff9b::/96 with embedded IPv4.
    [Test]
    [Arguments("64:ff9b::169.254.169.254")] // AWS metadata via DNS64
    [Arguments("64:ff9b::10.0.0.1")]        // RFC 1918 via DNS64
    [Arguments("64:ff9b::127.0.0.1")]       // loopback via DNS64
    public async Task IsPrivateOrReserved_BlocksDns64EmbeddedPrivate(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsTrue();
    }

    [Test]
    [Arguments("64:ff9b::8.8.8.8")]
    [Arguments("64:ff9b::1.1.1.1")]
    public async Task IsPrivateOrReserved_AllowsDns64EmbeddedPublic(string ipText)
    {
        var ip = IPAddress.Parse(ipText);
        await Assert.That(SsrfIpFilter.IsPrivateOrReserved(ip)).IsFalse();
    }
}
