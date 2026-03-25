namespace Snakk.Realtime.Services;

public interface IAccessVerifier
{
    Task<bool> VerifyDiscussionAccessAsync(string userId, string discussionId);
    Task<bool> VerifySpaceAccessAsync(string userId, string spacePublicId);
    Task<bool> VerifyHubAccessAsync(string userId, string hubPublicId);
}
