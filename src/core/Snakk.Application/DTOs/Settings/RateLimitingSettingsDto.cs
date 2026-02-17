namespace Snakk.Application.DTOs.Settings;

public class RateLimitingSettingsDto
{
    // Auth endpoints (login, register, password reset)
    public int AuthPermitLimit { get; set; } = 5;
    public int AuthWindowMinutes { get; set; } = 15;

    // API endpoints (general API calls)
    public int ApiPermitLimit { get; set; } = 100;
    public int ApiWindowMinutes { get; set; } = 1;

    // Expensive operations (search, reports, exports)
    public int ExpensivePermitLimit { get; set; } = 10;
    public int ExpensiveWindowMinutes { get; set; } = 1;
}
