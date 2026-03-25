namespace Snakk.Realtime.Services;

public class HttpAccessVerifier(HttpClient httpClient, ILogger<HttpAccessVerifier> logger) : IAccessVerifier
{
    public Task<bool> VerifyDiscussionAccessAsync(string userId, string discussionId) =>
        VerifyAsync(userId, "Discussion", discussionId);

    public Task<bool> VerifySpaceAccessAsync(string userId, string spacePublicId) =>
        VerifyAsync(userId, "Space", spacePublicId);

    public Task<bool> VerifyHubAccessAsync(string userId, string hubPublicId) =>
        VerifyAsync(userId, "Hub", hubPublicId);

    private async Task<bool> VerifyAsync(string userId, string scopeType, string scopeId)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/internal/realtime/verify-subscription",
                new { userId, scopeType, scopeId });

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Access verification failed for {ScopeType}:{ScopeId}", scopeType, scopeId);

            // Fail open for public content — prevents outage if API is briefly unavailable
            return true;
        }
    }
}
