using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Services;
using Snakk.Protos.Space;

namespace Snakk.Web.Pages;

/// <summary>
/// Universal "New Discussion" entry page at /new.
/// Accepts optional query params to scope the space selector:
///   ?spaceId=...                    — pre-populate with a specific space
///   ?hubId=...                      — scope search to a hub
///   ?communityId=...                — scope search to a community
///   (none)                          — search all spaces
/// </summary>
public class NewDiscussionModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    // Pre-select a space (passed to JS for client-side lookup)
    public string? PreselectedSpaceId { get; set; }

    // Scope for the space search (passed to JS)
    public string? ScopeHubId { get; set; }
    public string? ScopeCommunityId { get; set; }
    public string? ScopeCommunityName { get; set; }
    public string? ScopeHubName { get; set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string? spaceId,
        [FromQuery] string? hubId,
        [FromQuery] string? communityId)
    {
        PreselectedSpaceId = spaceId;
        ScopeHubId = hubId;
        ScopeCommunityId = communityId;

        // Resolve scope names for the UI label
        if (!string.IsNullOrEmpty(hubId))
        {
            var hub = await _apiClient.GetHubAsync(hubId);
            if (hub is not null)
            {
                ScopeHubName = hub.Name;
                // Also resolve the parent community
                var community = await _apiClient.GetCommunityAsync(hub.CommunityId);
                ScopeCommunityName = community?.Name;
            }
        }
        else if (!string.IsNullOrEmpty(communityId))
        {
            var community = await _apiClient.GetCommunityAsync(communityId);
            ScopeCommunityName = community?.Name;
        }

        return Page();
    }

    public static string TypeSlugFromInt(int type) => type switch
    {
        0 => "standard",
        1 => "question",
        2 => "poll",
        3 => "banner",
        4 => "link",
        5 => "images",
        6 => "guide",
        7 => "debate",
        8 => "journal",
        9 => "iama",
        _ => "standard"
    };

    public static (string Description, string Features) TypeInfo(int type) => type switch
    {
        0 => ("Start a conversation, share thoughts, or discuss a topic with the community.",
              "Replies in chronological order, rich text formatting, reactions"),
        1 => ("Ask the community a question. The best answer can be marked as the accepted solution.",
              "Accepted answer highlighting, mark as solved"),
        2 => ("Put a question to a vote. Add options and see how the community feels in real time.",
              "Single or multi-vote, optional end date, live results"),
        3 => ("", ""),
        4 => ("Share an interesting link with the community and start a discussion around it.",
              "URL with preview, commentary, link metadata"),
        5 => ("Share a collection of images with descriptions.",
              "Image uploads, captions, layout options"),
        6 => ("Write a structured how-to guide or tutorial with step-by-step instructions.",
              "Summary, prerequisites, numbered steps"),
        7 => ("Pose a topic and let the community argue different sides.",
              "2-3 defined positions, colored labels, filter by side"),
        8 => ("A living thread where the author posts timestamped updates over time.",
              "Append-only updates, progress tracking"),
        9 => ("Host an Ask Me Anything session. The community asks questions and you answer live.",
              "Official answers, best questions highlight, verification note"),
        _ => ("", "")
    };
}
