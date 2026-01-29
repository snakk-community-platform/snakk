using System.Net.Http.Json;

namespace Snakk.Web.Extensions;

/// <summary>
/// Extension methods for HttpClient with safe error handling
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Safely fetch JSON from an endpoint, returning null on any error
    /// </summary>
    public static async Task<T?> GetFromJsonAsyncSafe<T>(this HttpClient client, string requestUri) where T : class
    {
        try
        {
            return await client.GetFromJsonAsync<T>(requestUri);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely fetch JSON from an endpoint with cancellation token, returning null on any error
    /// </summary>
    public static async Task<T?> GetFromJsonAsyncSafe<T>(this HttpClient client, string requestUri, CancellationToken cancellationToken) where T : class
    {
        try
        {
            return await client.GetFromJsonAsync<T>(requestUri, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
