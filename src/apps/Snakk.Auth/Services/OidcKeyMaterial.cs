namespace Snakk.Auth.Services;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Loads (or creates on first run) the persisted certificates OpenIddict uses to sign and
/// encrypt the OIDC tokens it issues. Replaces the ephemeral, in-memory keys that were lost
/// on every restart — which invalidated all outstanding authorization codes/tokens and
/// prevented running more than one Auth instance (S19 in the 2026-06 security audit).
///
/// The cert is a self-signed RSA-2048 key persisted as a PKCS#12 file in the shared config
/// directory (same trust boundary as the rest of the app's secrets). Signing stays
/// asymmetric so OIDC clients (e.g. the MAUI app) can verify id_token signatures.
/// </summary>
public static class OidcKeyMaterial
{
    public static X509Certificate2 LoadOrCreate(string pfxPath, string subject, X509KeyUsageFlags usage)
    {
        if (File.Exists(pfxPath))
            return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(pfxPath), password: null);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5));

        var pfxBytes = cert.Export(X509ContentType.Pfx);
        File.WriteAllBytes(pfxPath, pfxBytes);
        return X509CertificateLoader.LoadPkcs12(pfxBytes, password: null);
    }
}
