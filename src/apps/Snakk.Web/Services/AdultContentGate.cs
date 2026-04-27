namespace Snakk.Web.Services;

using Snakk.Shared.Enums;

public enum AdultContentState
{
    Allowed,
    NeedsPrompt,
    Hidden
}

public static class AdultContentGate
{
    public const string ConfirmedCookie = "snakk.adult-confirmed";
    public const string DeclinedCookie = "snakk.adult-declined";

    public static AdultContentState GetState(
        HttpContext context,
        bool? userPreference,
        bool contentIsAdult)
    {
        if (!contentIsAdult)
            return AdultContentState.Allowed;

        if (userPreference == true)
            return AdultContentState.Allowed;

        if (userPreference == false)
            return AdultContentState.Hidden;

        if (context.Request.Cookies.ContainsKey(ConfirmedCookie))
            return AdultContentState.Allowed;

        if (context.Request.Cookies.ContainsKey(DeclinedCookie))
            return AdultContentState.Hidden;

        return AdultContentState.NeedsPrompt;
    }

    public static bool IsAllowed(HttpContext context) =>
        context.Request.Cookies.ContainsKey(ConfirmedCookie);

    /// <summary>
    /// Returns true when the viewer is allowed to see adult content for list-filtering purposes:
    /// authenticated users with AllowAdultContent == true, or anonymous users with the confirm cookie.
    /// Users who haven't decided yet (null preference, no cookies) count as NOT allowed — lists hide
    /// adult content by default, the gate prompt is reserved for direct page visits.
    /// </summary>
    public static bool ViewerAllowsAdult(HttpContext context, bool? userPreference)
    {
        if (userPreference == true) return true;
        if (userPreference == false) return false;
        return context.Request.Cookies.ContainsKey(ConfirmedCookie);
    }

    /// <summary>
    /// Convenience overload that fetches the current user's adult-content preference
    /// from the API when the viewer is authenticated. Anonymous viewers fall back to
    /// the confirm-cookie check. Used to decide whether the per-community "Hide adult
    /// discussions from lists" filter should be applied.
    /// </summary>
    public static async Task<bool> ViewerAllowsAdultAsync(HttpContext context, SnakkApiClient apiClient)
    {
        bool? userPreference = null;
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await apiClient.GetCurrentUserAsync();
            userPreference = user?.HasAllowAdultContent == true ? user.AllowAdultContent : null;
        }
        return ViewerAllowsAdult(context, userPreference);
    }

    private const string AdultPreviewImageModeItemKey = "snakk.adultPreviewImageMode";

    /// <summary>
    /// Returns the authenticated user's preferred preview-image mode for adult discussions.
    /// Anonymous viewers always get Show (they don't see adult content in lists). Result is
    /// cached on HttpContext.Items per request to avoid extra API round-trips.
    /// </summary>
    public static async Task<AdultPreviewImageModeEnum> GetAdultPreviewImageModeAsync(
        HttpContext context,
        SnakkApiClient apiClient)
    {
        if (context.Items.TryGetValue(AdultPreviewImageModeItemKey, out var cached)
            && cached is AdultPreviewImageModeEnum cachedMode)
            return cachedMode;

        var mode = AdultPreviewImageModeEnum.Show;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await apiClient.GetCurrentUserAsync();
            if (user is not null)
                mode = (AdultPreviewImageModeEnum)user.AdultPreviewImageMode;
        }

        context.Items[AdultPreviewImageModeItemKey] = mode;
        return mode;
    }
}
