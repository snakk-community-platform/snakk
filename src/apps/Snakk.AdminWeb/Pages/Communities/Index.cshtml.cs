using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.AdminWeb.Pages.Communities;

[Authorize(Roles = "GlobalAdmin,CommunityAdmin")]
public class IndexModel : PageModel
{
    public List<CommunityListItem> Communities { get; set; } = new();

    public void OnGet()
    {
        // TODO: Fetch from API
        Communities = new List<CommunityListItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Technology",
                Slug = "technology",
                Description = "All things tech - programming, gadgets, innovation",
                MemberCount = 15420,
                PostCount = 3421,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Gaming",
                Slug = "gaming",
                Description = "Discussion about video games, esports, and gaming culture",
                MemberCount = 23100,
                PostCount = 8934,
                CreatedAt = DateTime.UtcNow.AddMonths(-12),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Science",
                Slug = "science",
                Description = "Scientific discussions, research, and discoveries",
                MemberCount = 8750,
                PostCount = 2156,
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                IsActive = true
            }
        };
    }

    public record CommunityListItem
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int MemberCount { get; init; }
        public int PostCount { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool IsActive { get; init; }
    }
}
