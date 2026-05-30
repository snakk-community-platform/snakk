namespace Snakk.Shared.Helpers;

/// <summary>
/// Process-global toggle for the <c>Secure</c> attribute on auth/session cookies.
/// Default <c>true</c> (production-safe). Set once at startup from configuration:
/// <code>
/// AuthCookieSecurity.RequireSecure =
///     config.GetValue&lt;bool?&gt;("Cookies:RequireSecure") ?? !env.IsDevelopment();
/// </code>
/// The dev/test stack runs over plain HTTP, which silently drops Secure cookies and
/// breaks every authenticated flow (and authenticated load tests). Disabling it in
/// Development lets auth work over HTTP; all other environments stay Secure.
/// </summary>
public static class AuthCookieSecurity
{
    public static bool RequireSecure { get; set; } = true;
}
