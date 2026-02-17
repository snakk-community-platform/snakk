namespace Snakk.Api.Helpers;

/// <summary>
/// Helper for audit logging authentication events
/// </summary>
public static class AuthAuditLogger
{
    public static void LogLoginSuccess(ILogger logger, string email, string ipAddress, string? userAgent)
    {
        logger.LogInformation(
            "[AUTH AUDIT] Login success - Email: {Email}, IP: {IpAddress}, UserAgent: {UserAgent}",
            email, ipAddress, userAgent);
    }

    public static void LogLoginFailure(ILogger logger, string email, string ipAddress, string? userAgent)
    {
        logger.LogWarning(
            "[AUTH AUDIT] Login failure - Email: {Email}, IP: {IpAddress}, UserAgent: {UserAgent}",
            email, ipAddress, userAgent);
    }

    public static void LogRegistration(ILogger logger, string email, string ipAddress, string? userAgent)
    {
        logger.LogInformation(
            "[AUTH AUDIT] New registration - Email: {Email}, IP: {IpAddress}, UserAgent: {UserAgent}",
            email, ipAddress, userAgent);
    }

    public static void LogOAuthLogin(ILogger logger, string provider, string email, string ipAddress, string? userAgent)
    {
        logger.LogInformation(
            "[AUTH AUDIT] OAuth login - Provider: {Provider}, Email: {Email}, IP: {IpAddress}, UserAgent: {UserAgent}",
            provider, email, ipAddress, userAgent);
    }

    public static void LogLogout(ILogger logger, string userId, string ipAddress, string? userAgent)
    {
        logger.LogInformation(
            "[AUTH AUDIT] Logout - UserId: {UserId}, IP: {IpAddress}, UserAgent: {UserAgent}",
            userId, ipAddress, userAgent);
    }

    public static void LogTokenRefresh(ILogger logger, string userId, string ipAddress, string? userAgent)
    {
        logger.LogInformation(
            "[AUTH AUDIT] Token refresh - UserId: {UserId}, IP: {IpAddress}, UserAgent: {UserAgent}",
            userId, ipAddress, userAgent);
    }

    public static void LogOAuthStateValidationFailure(ILogger logger, string provider, string ipAddress, string? userAgent)
    {
        logger.LogWarning(
            "[AUTH AUDIT] OAuth state validation failure (possible CSRF attempt) - Provider: {Provider}, IP: {IpAddress}, UserAgent: {UserAgent}",
            provider, ipAddress, userAgent);
    }

    public static string GetClientIp(HttpContext context)
    {
        // Try to get real IP from X-Forwarded-For header (if behind proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fall back to RemoteIpAddress
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static string? GetUserAgent(HttpContext context)
    {
        return context.Request.Headers["User-Agent"].FirstOrDefault();
    }
}
