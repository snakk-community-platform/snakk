namespace Snakk.Application.DTOs.Auth;

/// <summary>
/// Returned by REST <c>/auth/login</c> when the account has 2FA enabled.
/// No access/refresh token is issued — the client must complete the second
/// factor at <c>/auth/2fa/verify</c> with the pending token from this response.
/// </summary>
public class LoginTwoFactorRequiredResponse
{
    public bool RequiresTwoFactor { get; init; } = true;

    /// <summary>
    /// Short-lived (5 min) JWT bound to this user, signed with the access-token key
    /// but issued for a separate audience so it cannot be used as a session token.
    /// Required by <c>/auth/2fa/verify</c>.
    /// </summary>
    public required string TwoFactorPendingToken { get; init; }

    public required TwoFactorPendingUserInfo User { get; init; }
}

public record TwoFactorPendingUserInfo(string Email, string? DisplayName);
