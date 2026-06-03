namespace Snakk.Api.Models;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string? TurnstileToken = null);

public record LoginRequest(
    string Email,
    string Password,
    string? TurnstileToken = null);

public record UpdateProfileRequest(
    string DisplayName);

public record UpdatePreferencesRequest(
    bool? AutoFollowOnReply,
    string? Timezone = null);

public record RefreshTokenRequest(string RefreshToken);

public record ChangePasswordRequest(
    string NewPassword,
    string ConfirmPassword,
    string? SudoToken = null);

public record VerifyCredentialRequest(string? Password, string? TotpCode, string? SudoToken = null);

public record SudoRequest(string? Password, string? TotpCode);

public record SudoPasskeyCompleteRequest(string ChallengeId, string AssertionResponseJson, string? TotpCode = null);

public record RevokeSessionRequest(string? SudoToken);
public record RevokeAllOtherSessionsRequest(string? SudoToken, string ExcludeSessionId);

public record OAuthSudoNonceRequest(string Nonce);

public record DeleteAccountRequest(string? SudoToken, string Confirmation);
