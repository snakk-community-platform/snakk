namespace Snakk.Application.Services;

using Snakk.Application.DTOs.Auth;

public interface IPasskeyService
{
    // Returns (optionsJson, challengeId) — caller stores challengeId, browser uses optionsJson
    Task<(string OptionsJson, string ChallengeId)> BeginRegistrationAsync(
        string userPublicId, string userDisplayName, CancellationToken ct = default);

    Task CompleteRegistrationAsync(
        string userPublicId, string challengeId, string attestationJson,
        string? friendlyName, CancellationToken ct = default);

    // Returns (optionsJson, challengeId)
    Task<(string OptionsJson, string ChallengeId)> BeginLoginAsync(
        string? email, CancellationToken ct = default);

    // Returns the authenticated user's internal and public IDs on success
    Task<(int UserId, string PublicId)> CompleteLoginAsync(
        string challengeId, string assertionJson, CancellationToken ct = default);

    Task<List<PasskeyDto>> GetUserPasskeysAsync(
        string userPublicId, CancellationToken ct = default);

    Task DeletePasskeyAsync(
        string userPublicId, int credentialId, CancellationToken ct = default);
}
