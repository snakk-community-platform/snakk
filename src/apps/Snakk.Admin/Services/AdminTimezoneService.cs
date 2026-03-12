using Snakk.Protos.Auth;

namespace Snakk.Admin.Services;

/// <summary>
/// Scoped service (per Blazor Server circuit) that caches the site timezone
/// and provides timezone-aware date formatting helpers for admin components.
/// Call EnsureInitializedAsync() once per component load, then use sync format methods.
/// </summary>
public class AdminTimezoneService(AuthService.AuthServiceClient authClient)
{
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;
    private bool _initialized;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        try
        {
            var response = await authClient.GetPublicSettingsAsync(new GetPublicSettingsRequest());
            var tzId = string.IsNullOrEmpty(response.Timezone) ? "UTC" : response.Timezone;
            try { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { _tz = TimeZoneInfo.Utc; }
        }
        catch
        {
            _tz = TimeZoneInfo.Utc;
        }

        _initialized = true;
    }

    private DateTime ToLocal(long ticks)
        => TimeZoneInfo.ConvertTimeFromUtc(new DateTime(ticks, DateTimeKind.Utc), _tz);

    /// <summary>Format ticks as short date + short time (e.g., "3/15/2026 2:30 PM")</summary>
    public string FormatDateTime(long ticks) => ToLocal(ticks).ToString("g");

    /// <summary>Format optional ticks as short date + short time, or fallback string</summary>
    public string FormatDateTime(long? ticks, string fallback = "Permanent")
        => ticks.HasValue ? FormatDateTime(ticks.Value) : fallback;

    /// <summary>Format ticks as short date only (e.g., "3/15/2026")</summary>
    public string FormatDate(long ticks) => ToLocal(ticks).ToString("d");

    /// <summary>Format ticks as abbreviated month + day label (e.g., "Mar 15")</summary>
    public string FormatDateLabel(long ticks) => ToLocal(ticks).ToString("MMM d");
}
