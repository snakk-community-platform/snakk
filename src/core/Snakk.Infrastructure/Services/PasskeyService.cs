namespace Snakk.Infrastructure.Services;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Auth;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class PasskeyService(
    IFido2 fido2,
    SnakkDbContext context,
    IMemoryCache cache,
    ILogger<PasskeyService> logger) : IPasskeyService
{
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions TransportJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<(string OptionsJson, string ChallengeId)> BeginRegistrationAsync(
        string userPublicId, string userDisplayName, CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Email })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("User not found");

        var existingCredentials = await context.PasskeyCredentials
            .Where(p => p.UserId == user.Id)
            .Select(p => p.CredentialId)
            .ToListAsync(ct);

        var excludeCredentials = existingCredentials
            .Select(id => new PublicKeyCredentialDescriptor(id))
            .ToList();

        var fido2User = new Fido2User
        {
            Id = Encoding.UTF8.GetBytes(userPublicId),
            Name = user.Email ?? userPublicId,
            DisplayName = userDisplayName
        };

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fido2User,
            ExcludeCredentials = excludeCredentials,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred
            }
        });

        var challengeId = Base64UrlEncode(options.Challenge);
        cache.Set(CacheKey("reg", challengeId), options.ToJson(), ChallengeTtl);

        logger.LogDebug("Passkey registration started for user {PublicId}", userPublicId);
        return (options.ToJson(), challengeId);
    }

    public async Task CompleteRegistrationAsync(
        string userPublicId, string challengeId, string attestationJson,
        string? friendlyName, CancellationToken ct = default)
    {
        var cachedJson = cache.Get<string>(CacheKey("reg", challengeId))
            ?? throw new InvalidOperationException("Registration challenge not found or expired");

        cache.Remove(CacheKey("reg", challengeId));

        var originalOptions = CredentialCreateOptions.FromJson(cachedJson);
        var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationJson)
            ?? throw new InvalidOperationException("Invalid attestation response");

        var user = await context.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("User not found");

        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = originalOptions,
            IsCredentialIdUniqueToUserCallback = async (args, token) =>
            {
                var credId = args.CredentialId;
                return !await context.PasskeyCredentials
                    .AnyAsync(p => p.CredentialId == credId, token);
            }
        }, ct);

        var transports = result.Transports is { Length: > 0 }
            ? JsonSerializer.Serialize(result.Transports, TransportJsonOptions)
            : null;

        context.PasskeyCredentials.Add(new PasskeyCredentialDatabaseEntity
        {
            UserId = user.Id,
            CredentialId = result.Id,
            PublicKey = result.PublicKey,
            SignCount = result.SignCount,
            Transports = transports,
            AaGuid = result.AaGuid,
            FriendlyName = friendlyName,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Passkey registered for user {PublicId}", userPublicId);
    }

    public async Task<(string OptionsJson, string ChallengeId)> BeginLoginAsync(
        string? email, CancellationToken ct = default)
    {
        List<PublicKeyCredentialDescriptor> allowedCredentials = [];

        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await context.Users
                .Where(u => u.Email == email && !u.IsDeleted)
                .Select(u => new { u.Id })
                .FirstOrDefaultAsync(ct);

            if (user is not null)
            {
                var credIds = await context.PasskeyCredentials
                    .Where(p => p.UserId == user.Id)
                    .Select(p => p.CredentialId)
                    .ToListAsync(ct);

                allowedCredentials = credIds
                    .Select(id => new PublicKeyCredentialDescriptor(id))
                    .ToList();
            }
        }

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        var challengeId = Base64UrlEncode(options.Challenge);
        cache.Set(CacheKey("login", challengeId), options.ToJson(), ChallengeTtl);

        return (options.ToJson(), challengeId);
    }

    public async Task<(int UserId, string PublicId)> CompleteLoginAsync(
        string challengeId, string assertionJson, CancellationToken ct = default)
    {
        var cachedJson = cache.Get<string>(CacheKey("login", challengeId))
            ?? throw new InvalidOperationException("Login challenge not found or expired");

        cache.Remove(CacheKey("login", challengeId));

        var originalOptions = AssertionOptions.FromJson(cachedJson);
        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionJson)
            ?? throw new InvalidOperationException("Invalid assertion response");

        var rawId = assertionResponse.RawId;
        var credential = await context.PasskeyCredentials
            .AsTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.CredentialId == rawId, ct)
            ?? throw new InvalidOperationException("Credential not found");

        if (credential.User.IsDeleted)
            throw new InvalidOperationException("Account not found");

        var expectedUserHandle = Encoding.UTF8.GetBytes(credential.User.PublicId);

        var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = assertionResponse,
            OriginalOptions = originalOptions,
            StoredPublicKey = credential.PublicKey,
            StoredSignatureCounter = credential.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = async (args, token) =>
            {
                if (!args.UserHandle.SequenceEqual(expectedUserHandle))
                    return false;
                var cId = args.CredentialId;
                return await context.PasskeyCredentials
                    .AnyAsync(p => p.CredentialId == cId && p.UserId == credential.UserId, token);
            }
        }, ct);

        credential.SignCount = result.SignCount;
        credential.LastUsedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Passkey login succeeded for user {PublicId}", credential.User.PublicId);
        return (credential.UserId, credential.User.PublicId);
    }

    public async Task<List<PasskeyDto>> GetUserPasskeysAsync(
        string userPublicId, CancellationToken ct = default)
    {
        return await context.PasskeyCredentials
            .Where(p => p.User.PublicId == userPublicId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PasskeyDto(
                p.Id,
                p.FriendlyName,
                p.CreatedAt,
                p.LastUsedAt,
                p.Transports,
                p.AaGuid))
            .ToListAsync(ct);
    }

    public async Task DeletePasskeyAsync(
        string userPublicId, int credentialId, CancellationToken ct = default)
    {
        var credential = await context.PasskeyCredentials
            .AsTracking()
            .FirstOrDefaultAsync(
                p => p.Id == credentialId && p.User.PublicId == userPublicId, ct)
            ?? throw new InvalidOperationException("Passkey not found");

        context.PasskeyCredentials.Remove(credential);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Passkey {CredentialId} deleted for user {PublicId}",
            credentialId, userPublicId);
    }

    private static string CacheKey(string type, string challengeId) =>
        $"passkey:{type}:{challengeId}";

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
