namespace Snakk.Web.Helpers;

/// <summary>
/// Helper methods for building URIs with query parameters
/// </summary>
public static class UriHelper
{
    /// <summary>
    /// Build a URL with query parameters, automatically filtering out null/empty values
    /// </summary>
    /// <param name="baseUrl">Base URL path</param>
    /// <param name="parameters">Tuples of (key, value) pairs</param>
    /// <returns>URL with query string</returns>
    public static string BuildQuery(string baseUrl, params (string key, string? value)[] parameters)
    {
        var queryParams = parameters
            .Where(p => !string.IsNullOrEmpty(p.value))
            .Select(p => $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.value!)}")
            .ToList();

        if (queryParams.Count == 0)
            return baseUrl;

        return $"{baseUrl}?{string.Join("&", queryParams)}";
    }
}
