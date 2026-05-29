using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Snakk.Auth.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Snakk.Auth.Tests.Services;

/// <summary>
/// Regression coverage for CR-21: <c>OAuth/Challenge.cshtml.cs</c> previously parsed
/// the <c>.Snakk.Auth</c> cookie with <c>JwtSecurityTokenHandler.ReadJwtToken</c>
/// (no signature check) and let an attacker forge a JWT carrying the victim's
/// <c>sub</c> claim, drop it into the cookie, and drive the OAuth connect flow
/// against the victim's account. <see cref="JwtCookieValidator"/> must reject any
/// token that isn't HS256-signed with the configured key for the configured issuer
/// and audience.
/// </summary>
public class JwtCookieValidatorTests
{
    private const string Secret = "test-secret-with-at-least-32-chars-of-entropy!!";
    private const string Issuer = "Snakk";
    private const string Audience = "Snakk";
    private const string ValidUserId = "victim-public-id";

    private static JwtCookieValidator NewValidator(
        string secret = Secret, string issuer = Issuer, string audience = Audience)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = secret,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience
            })
            .Build();
        return new JwtCookieValidator(config);
    }

    private static string IssueHs256(
        string userId = ValidUserId,
        string secret = Secret,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? exp = null)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId)],
            expires: exp ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string IssueAlgNone(string userId = ValidUserId)
    {
        // Build the JWT structure manually with `alg: none` and no signature segment.
        // JwtSecurityTokenHandler refuses to write tokens without a signing key, so
        // we construct it from base64url segments directly.
        var header = "{\"alg\":\"none\",\"typ\":\"JWT\"}";
        var payload = $"{{\"sub\":\"{userId}\",\"nameid\":\"{userId}\",\"iss\":\"{Issuer}\",\"aud\":\"{Audience}\",\"exp\":{DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()}}}";
        return $"{Base64Url(header)}.{Base64Url(payload)}.";
    }

    private static string Base64Url(string s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Test]
    public async Task ValidatesProperlySignedToken_ReturnsUserId()
    {
        var token = IssueHs256();
        var result = NewValidator().ValidateAndExtractUserId(token);
        await Assert.That(result).IsEqualTo(ValidUserId);
    }

    // The original vulnerability: an attacker forges a JWT signed with the WRONG key
    // (or no key) and inserts the victim's sub. Pre-fix this returned the victim's
    // ID; post-fix it must return null.
    [Test]
    public async Task RejectsTokenSignedWithWrongKey()
    {
        var forged = IssueHs256(secret: "attacker-fabricated-key-also-32-chars-long");
        var result = NewValidator().ValidateAndExtractUserId(forged);
        await Assert.That(result).IsNull();
    }

    // alg=none is the canonical JWT downgrade attack. The validator must require a
    // signed token AND restrict to HS256.
    [Test]
    public async Task RejectsAlgNoneToken()
    {
        var forged = IssueAlgNone();
        var result = NewValidator().ValidateAndExtractUserId(forged);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RejectsExpiredToken()
    {
        var token = IssueHs256(exp: DateTime.UtcNow.AddMinutes(-10));
        var result = NewValidator().ValidateAndExtractUserId(token);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RejectsWrongIssuer()
    {
        var token = IssueHs256(issuer: "evil.example.com");
        var result = NewValidator().ValidateAndExtractUserId(token);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RejectsWrongAudience()
    {
        var token = IssueHs256(audience: "Other");
        var result = NewValidator().ValidateAndExtractUserId(token);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RejectsMalformedToken()
    {
        var result = NewValidator().ValidateAndExtractUserId("not.a.jwt");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RejectsNullAndEmpty()
    {
        var v = NewValidator();
        await Assert.That(v.ValidateAndExtractUserId(null)).IsNull();
        await Assert.That(v.ValidateAndExtractUserId("")).IsNull();
        await Assert.That(v.ValidateAndExtractUserId("    ")).IsNull();
    }
}
