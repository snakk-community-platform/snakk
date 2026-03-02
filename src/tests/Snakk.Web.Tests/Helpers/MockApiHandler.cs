using System.Net;
using System.Text;
using System.Text.Json;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// A mock HTTP message handler that intercepts outgoing HttpClient requests
/// and returns preconfigured responses. Used to replace real API calls in tests.
/// </summary>
public class MockApiHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
    private readonly List<HttpRequestMessage> _receivedRequests = [];

    /// <summary>
    /// All requests that were sent through this handler, for assertion purposes.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> ReceivedRequests => _receivedRequests;

    /// <summary>
    /// Configure a response for a specific path pattern (matched by StartsWith, case-insensitive).
    /// The path should not include the base URL — just the relative path (e.g., "/auth/status").
    /// </summary>
    public void SetupResponse(string pathPrefix, HttpStatusCode statusCode, object? body = null)
    {
        _responses[pathPrefix.ToLowerInvariant()] = _ =>
        {
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
            {
                var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return response;
        };
    }

    /// <summary>
    /// Configure a response for a specific path pattern using a factory function
    /// that receives the request and can inspect it before returning a response.
    /// </summary>
    public void SetupResponse(string pathPrefix, Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _responses[pathPrefix.ToLowerInvariant()] = factory;
    }

    /// <summary>
    /// Configure a response that returns a JSON body with 200 OK.
    /// </summary>
    public void SetupJsonResponse(string pathPrefix, object body)
    {
        SetupResponse(pathPrefix, HttpStatusCode.OK, body);
    }

    /// <summary>
    /// Clears all configured responses and recorded requests.
    /// </summary>
    public void Reset()
    {
        _responses.Clear();
        _receivedRequests.Clear();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _receivedRequests.Add(request);

        var path = request.RequestUri?.PathAndQuery?.ToLowerInvariant() ?? "";

        // Find the best matching response (longest prefix wins)
        var bestMatch = _responses
            .Where(kvp => path.StartsWith(kvp.Key))
            .OrderByDescending(kvp => kvp.Key.Length)
            .FirstOrDefault();

        if (bestMatch.Value is not null)
            return Task.FromResult(bestMatch.Value(request));

        // Default: return 404 for unmatched paths
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = "No mock configured", path }),
                Encoding.UTF8,
                "application/json")
        });
    }
}
