namespace Snakk.Auth.Services;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Binds the second-factor verification step to the browser session that just passed the
/// first-factor identity check (password, OAuth, or passkey). Without this, the 2FA page
/// trusted an <c>email</c> taken straight from the query string, so anyone who knew a
/// victim's email could POST TOTP guesses directly to <c>/auth/twofactorverify</c> without
/// ever proving the password (S3 in the 2026-06 security audit).
/// </summary>
public static class TwoFactorLoginSession
{
    private const string EmailKey = "2fa.pending.email";

    /// <summary>Records, server-side, which account just passed first-factor auth.</summary>
    public static void Begin(ISession session, string email) =>
        session.SetString(EmailKey, email);

    /// <summary>The pending account's email, or <c>null</c> if no first-factor step happened.</summary>
    public static string? GetEmail(ISession session) =>
        session.GetString(EmailKey);

    /// <summary>Clears the pending state once verification succeeds.</summary>
    public static void Clear(ISession session) =>
        session.Remove(EmailKey);
}
