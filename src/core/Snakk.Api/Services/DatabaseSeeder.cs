namespace Snakk.Api.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class DatabaseSeeder(SnakkDbContext context, IPasswordHasher passwordHasher)
{
    private readonly SnakkDbContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly Random _random = new(42); // Fixed seed for reproducibility

    public async Task SeedAsync()
    {
        // Always ensure test user and default admin exist
        await EnsureTestUserExistsAsync();
        await EnsureDefaultAdminExistsAsync();

        // Check if full seeding was already done (look for test communities with custom domains)
        var hasTestCommunities = await _context.CommunityDomains.AnyAsync(d => d.Domain == "test1.snakk.local");
        if (hasTestCommunities)
        {
            Console.WriteLine("Database already fully seeded. Skipping.");
            return;
        }

        // Delete existing data and reseed (since we need the full dataset)
        Console.WriteLine("Clearing existing data for full reseed...");
        await ClearExistingDataAsync();

        // Create users first
        var users = await SeedUsersAsync();

        // Create communities with custom domains
        var snakkCommunity = await CreateSnakkCommunityAsync(users);
        var test1Community = await CreateTest1CommunityAsync(users);
        var test2Community = await CreateTest2CommunityAsync(users);
        var test3Community = await CreateTest3CommunityAsync(users);

        Console.WriteLine("Database seeding completed successfully.");
    }

    private async Task ClearExistingDataAsync()
    {
        // Delete in correct order due to foreign keys
        _context.Posts.RemoveRange(_context.Posts);
        _context.Discussions.RemoveRange(_context.Discussions);
        _context.Spaces.RemoveRange(_context.Spaces);
        _context.Hubs.RemoveRange(_context.Hubs);
        _context.CommunityDomains.RemoveRange(_context.CommunityDomains);
        _context.Communities.RemoveRange(_context.Communities);
        // Keep the test user, delete others
        var usersToDelete = _context.Users.Where(u => u.PublicId != "01JJQP0000000000000000TEST");
        _context.Users.RemoveRange(usersToDelete);
        await _context.SaveChangesAsync();
        Console.WriteLine("Existing data cleared.");
    }

    private async Task EnsureTestUserExistsAsync()
    {
        const string testUserId = "01JJQP0000000000000000TEST";

        var exists = await _context.Users.AnyAsync(u => u.PublicId == testUserId);
        if (exists)
            return;

        var testUser = new UserDatabaseEntity
        {
            PublicId = testUserId,
            DisplayName = "Test User",
            Email = "test@snakk.dev",
            CreatedAt = DateTime.UtcNow.AddDays(-365),
            LastSeenAt = DateTime.UtcNow
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();
        Console.WriteLine("Test user created.");
    }

    private async Task EnsureDefaultAdminExistsAsync()
    {
        const string adminEmail = "admin@snakk.local";

        var exists = await _context.AdminUsers.AnyAsync(a => a.Email == adminEmail);
        if (exists)
            return;

        // Default password: "admin123" (should be changed on first login in production!)
        var passwordHash = _passwordHasher.HashPassword("admin123");

        var adminUser = new AdminUserDatabaseEntity
        {
            PublicId = "a_01JJQP0000000000000ADMIN",
            Email = adminEmail,
            PasswordHash = passwordHash,
            DisplayName = "Admin User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminUsers.Add(adminUser);
        await _context.SaveChangesAsync();
        Console.WriteLine($"Default admin user created: {adminEmail} / admin123");
    }

    private async Task<List<UserDatabaseEntity>> SeedUsersAsync()
    {
        var users = new List<UserDatabaseEntity>();

        // Get the test user
        var testUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == "01JJQP0000000000000000TEST");
        if (testUser != null)
            users.Add(testUser);

        var userNames = new[]
        {
            "Alice Johnson", "Bob Smith", "Charlie Davis", "Diana Prince",
            "Ethan Hunt", "Fiona Shaw", "George Miller", "Hannah Lee",
            "Ian Malcolm", "Julia Roberts", "Kevin Hart", "Laura Palmer",
            "Michael Scott", "Nina Simone", "Oscar Wilde", "Paula Abdul",
            "Quincy Jones", "Rachel Green", "Steve Rogers", "Tina Fey",
            "Uma Thurman", "Victor Hugo", "Wendy Darling", "Xavier Chen",
            "Yuki Tanaka", "Zoe Barnes", "Adam West", "Beth March",
            "Carl Sagan", "Dana Scully"
        };

        foreach (var name in userNames)
        {
            var user = new UserDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                DisplayName = name,
                Email = $"{name.Replace(" ", ".").ToLower()}@example.com",
                CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(30, 365)),
                LastSeenAt = DateTime.UtcNow.AddDays(-_random.Next(0, 14))
            };
            users.Add(user);
            _context.Users.Add(user);
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"Created {users.Count} users.");
        return users;
    }

    // ===== MAIN SNAKK COMMUNITY =====
    private async Task<CommunityDatabaseEntity> CreateSnakkCommunityAsync(List<UserDatabaseEntity> users)
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = "01JJQP0000000000000SNAKK",
            Name = "Snakk",
            Slug = "snakk",
            Description = "The main Snakk community - discuss everything!",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-365)
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Hub 1: Technology (large - 4 spaces, heavily used)
        var techHub = await CreateHubAsync(community, "Technology", "technology", "All things tech");
        var webDevSpace = await CreateSpaceAsync(techHub, "Web Development", "web-dev", "Frontend, backend, full-stack");
        var mobileSpace = await CreateSpaceAsync(techHub, "Mobile Apps", "mobile", "iOS, Android, cross-platform");
        var aiSpace = await CreateSpaceAsync(techHub, "AI & Machine Learning", "ai-ml", "Neural networks, LLMs, data science");
        var devOpsSpace = await CreateSpaceAsync(techHub, "DevOps & Cloud", "devops", "AWS, Azure, Kubernetes, CI/CD");

        // Create discussions with uneven distribution
        await CreateDiscussionsForSpace(webDevSpace, users, 45); // Very active
        await CreateDiscussionsForSpace(mobileSpace, users, 18);
        await CreateDiscussionsForSpace(aiSpace, users, 67);     // Most active
        await CreateDiscussionsForSpace(devOpsSpace, users, 12);

        // Hub 2: Gaming (medium - 3 spaces)
        var gamingHub = await CreateHubAsync(community, "Gaming", "gaming", "Video games and esports");
        var pcGamingSpace = await CreateSpaceAsync(gamingHub, "PC Gaming", "pc", "Steam, Epic, GOG discussions");
        var consoleSpace = await CreateSpaceAsync(gamingHub, "Console Gaming", "console", "PlayStation, Xbox, Nintendo");
        var indieSpace = await CreateSpaceAsync(gamingHub, "Indie Games", "indie", "Hidden gems and indie devs");

        await CreateDiscussionsForSpace(pcGamingSpace, users, 31);
        await CreateDiscussionsForSpace(consoleSpace, users, 24);
        await CreateDiscussionsForSpace(indieSpace, users, 8);   // Less active

        // Hub 3: Science (small - 2 spaces, niche)
        var scienceHub = await CreateHubAsync(community, "Science", "science", "Scientific discussions");
        var physicsSpace = await CreateSpaceAsync(scienceHub, "Physics", "physics", "Quantum to cosmos");
        var biologySpace = await CreateSpaceAsync(scienceHub, "Biology", "biology", "Life sciences");

        await CreateDiscussionsForSpace(physicsSpace, users, 15);
        await CreateDiscussionsForSpace(biologySpace, users, 7);

        Console.WriteLine("Created Snakk community with 3 hubs, 9 spaces.");
        return community;
    }

    // ===== TEST1 COMMUNITY (Small) =====
    private async Task<CommunityDatabaseEntity> CreateTest1CommunityAsync(List<UserDatabaseEntity> users)
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Test Community One",
            Slug = "test1",
            Description = "A small test community for custom domain testing",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-180)
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Add custom domain
        var domain = new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "test1.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-180)
        };
        _context.CommunityDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Single hub with 2 spaces
        var generalHub = await CreateHubAsync(community, "General", "general", "General discussions");
        var announcementsSpace = await CreateSpaceAsync(generalHub, "Announcements", "announcements", "Official announcements");
        var feedbackSpace = await CreateSpaceAsync(generalHub, "Feedback", "feedback", "Share your feedback");

        await CreateDiscussionsForSpace(announcementsSpace, users, 3);  // Very few
        await CreateDiscussionsForSpace(feedbackSpace, users, 11);

        Console.WriteLine("Created Test1 community (small) with custom domain test1.snakk.local");
        return community;
    }

    // ===== TEST2 COMMUNITY (Medium) =====
    private async Task<CommunityDatabaseEntity> CreateTest2CommunityAsync(List<UserDatabaseEntity> users)
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Test Community Two",
            Slug = "test2",
            Description = "A medium-sized test community for development",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-120)
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Add custom domain
        var domain = new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "test2.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-120)
        };
        _context.CommunityDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Hub 1: Discussion
        var discussionHub = await CreateHubAsync(community, "Discussion", "discussion", "Open discussions");
        var introSpace = await CreateSpaceAsync(discussionHub, "Introductions", "intro", "Say hello!");
        var chatSpace = await CreateSpaceAsync(discussionHub, "General Chat", "chat", "Off-topic conversations");
        var questionsSpace = await CreateSpaceAsync(discussionHub, "Q&A", "questions", "Ask anything");

        await CreateDiscussionsForSpace(introSpace, users, 22);
        await CreateDiscussionsForSpace(chatSpace, users, 38);   // Most active
        await CreateDiscussionsForSpace(questionsSpace, users, 15);

        // Hub 2: Projects
        var projectsHub = await CreateHubAsync(community, "Projects", "projects", "Show off your work");
        var showcaseSpace = await CreateSpaceAsync(projectsHub, "Showcase", "showcase", "Share your projects");
        var collabSpace = await CreateSpaceAsync(projectsHub, "Collaboration", "collab", "Find collaborators");

        await CreateDiscussionsForSpace(showcaseSpace, users, 9);
        await CreateDiscussionsForSpace(collabSpace, users, 4);  // Least active

        Console.WriteLine("Created Test2 community (medium) with custom domain test2.snakk.local");
        return community;
    }

    // ===== TEST3 COMMUNITY (Large) =====
    private async Task<CommunityDatabaseEntity> CreateTest3CommunityAsync(List<UserDatabaseEntity> users)
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Test Community Three",
            Slug = "test3",
            Description = "A large test community with lots of content",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-300)
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Add custom domain
        var domain = new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "test3.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-300)
        };
        _context.CommunityDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Hub 1: Learning (4 spaces)
        var learningHub = await CreateHubAsync(community, "Learning", "learning", "Educational content");
        var tutorialsSpace = await CreateSpaceAsync(learningHub, "Tutorials", "tutorials", "Step-by-step guides");
        var coursesSpace = await CreateSpaceAsync(learningHub, "Courses", "courses", "Recommended courses");
        var booksSpace = await CreateSpaceAsync(learningHub, "Books", "books", "Book recommendations");
        var resourcesSpace = await CreateSpaceAsync(learningHub, "Resources", "resources", "Useful links and tools");

        await CreateDiscussionsForSpace(tutorialsSpace, users, 52);  // Very active
        await CreateDiscussionsForSpace(coursesSpace, users, 19);
        await CreateDiscussionsForSpace(booksSpace, users, 27);
        await CreateDiscussionsForSpace(resourcesSpace, users, 41);

        // Hub 2: Community (3 spaces)
        var communityHub = await CreateHubAsync(community, "Community", "community", "Community matters");
        var eventsSpace = await CreateSpaceAsync(communityHub, "Events", "events", "Upcoming events");
        var metaSpace = await CreateSpaceAsync(communityHub, "Meta", "meta", "Discussions about the community");
        var helpSpace = await CreateSpaceAsync(communityHub, "Help Desk", "help", "Get help from the community");

        await CreateDiscussionsForSpace(eventsSpace, users, 6);
        await CreateDiscussionsForSpace(metaSpace, users, 14);
        await CreateDiscussionsForSpace(helpSpace, users, 33);

        // Hub 3: Creative (5 spaces - largest hub)
        var creativeHub = await CreateHubAsync(community, "Creative", "creative", "Creative works");
        var writingSpace = await CreateSpaceAsync(creativeHub, "Writing", "writing", "Stories, poems, essays");
        var artSpace = await CreateSpaceAsync(creativeHub, "Art", "art", "Visual art and design");
        var musicSpace = await CreateSpaceAsync(creativeHub, "Music", "music", "Music creation and appreciation");
        var photoSpace = await CreateSpaceAsync(creativeHub, "Photography", "photo", "Photo sharing and critique");
        var videoSpace = await CreateSpaceAsync(creativeHub, "Video", "video", "Video production");

        await CreateDiscussionsForSpace(writingSpace, users, 28);
        await CreateDiscussionsForSpace(artSpace, users, 63);    // Most popular in this hub
        await CreateDiscussionsForSpace(musicSpace, users, 17);
        await CreateDiscussionsForSpace(photoSpace, users, 44);
        await CreateDiscussionsForSpace(videoSpace, users, 11);

        Console.WriteLine("Created Test3 community (large) with custom domain test3.snakk.local");
        return community;
    }

    // ===== HELPER METHODS =====

    private async Task<HubDatabaseEntity> CreateHubAsync(
        CommunityDatabaseEntity community, string name, string slug, string description)
    {
        var hub = new HubDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Name = name,
            Slug = slug,
            Description = description,
            CreatedAt = community.CreatedAt.AddDays(_random.Next(1, 30))
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();
        return hub;
    }

    private async Task<SpaceDatabaseEntity> CreateSpaceAsync(
        HubDatabaseEntity hub, string name, string slug, string description)
    {
        var space = new SpaceDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            HubId = hub.Id,
            Name = name,
            Slug = slug,
            Description = description,
            CreatedAt = hub.CreatedAt.AddDays(_random.Next(1, 14))
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();
        return space;
    }

    private async Task CreateDiscussionsForSpace(
        SpaceDatabaseEntity space, List<UserDatabaseEntity> users, int count)
    {
        var discussionTitles = new[]
        {
            "Getting started with {0}",
            "Best practices for {0}",
            "Common mistakes in {0}",
            "Advanced {0} techniques",
            "Resources for {0}",
            "What's new in {0}?",
            "Tips and tricks: {0}",
            "Troubleshooting {0}",
            "My {0} experience",
            "Question about {0}",
            "How to improve {0}",
            "Tools for {0}",
            "The future of {0}",
            "Share your {0} work",
            "Help with {0}",
            "{0} for beginners",
            "Professional {0} advice",
            "Comparing {0} approaches",
            "Weekly {0} thread",
            "{0} discussion",
            "Thoughts on {0}?",
            "{0} recommendations",
            "Learning {0}",
            "{0} showcase",
            "Feedback on my {0}",
            "{0} news and updates",
            "Why I love {0}",
            "{0} challenges",
            "Beginner's guide to {0}",
            "{0} inspiration"
        };

        var postContents = new[]
        {
            "This is a great topic! I've been exploring this area recently.",
            "Thanks for sharing. Very helpful information!",
            "I agree with your perspective here.",
            "Has anyone tried a different approach?",
            "Here's what worked for me...",
            "Great discussion! I'd like to add my thoughts.",
            "I'm struggling with this, any tips?",
            "This reminds me of something I worked on before.",
            "Excellent explanation! Could you expand on that?",
            "I see it differently. Here's my take...",
            "Thanks for bringing this up. Important topic.",
            "Following along. Very interesting!",
            "I've documented my experience with this.",
            "Has this been validated by others?",
            "Looking forward to more like this!",
            "This solved my problem. Thank you!",
            "Can someone recommend more resources?",
            "I think there's a better approach here.",
            "Great community! Glad I found this.",
            "Update: Found a solution that works!",
            "Interesting perspective. I hadn't considered that.",
            "This is exactly what I was looking for.",
            "Could you share more details?",
            "I've been dealing with the same issue.",
            "Well written and informative!",
            "Adding my two cents here...",
            "This changed how I think about it.",
            "Saved for reference. Great stuff!",
            "Anyone else experiencing this?",
            "Let me share what I learned..."
        };

        for (int i = 0; i < count; i++)
        {
            var titleTemplate = discussionTitles[_random.Next(discussionTitles.Length)];
            var title = string.Format(titleTemplate, space.Name);
            var slug = GenerateSlug(title);
            var author = users[_random.Next(users.Count)];
            var createdAt = space.CreatedAt.AddDays(_random.Next(1, 150));
            var isPinned = _random.Next(100) < 3;  // 3% pinned
            var isLocked = _random.Next(100) < 1;  // 1% locked

            var discussion = new DiscussionDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                SpaceId = space.Id,
                Title = title,
                Slug = slug,
                CreatedByUserId = author.Id,
                CreatedAt = createdAt,
                LastActivityAt = createdAt,
                IsPinned = isPinned,
                IsLocked = isLocked
            };
            _context.Discussions.Add(discussion);
            await _context.SaveChangesAsync();

            // First post (opening post)
            var firstPost = new PostDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                DiscussionId = discussion.Id,
                Content = $"{postContents[_random.Next(postContents.Length)]}",
                CreatedByUserId = author.Id,
                CreatedAt = createdAt,
                IsFirstPost = true,
                RevisionCount = 0
            };
            _context.Posts.Add(firstPost);

            // Variable number of replies (0 to 25, skewed toward lower numbers)
            // Use exponential distribution for more realistic activity
            int replyCount = GetSkewedReplyCount();
            var lastActivityAt = createdAt;

            for (int j = 0; j < replyCount; j++)
            {
                var replyAuthor = users[_random.Next(users.Count)];
                var replyCreatedAt = lastActivityAt.AddMinutes(_random.Next(5, 60 * 24 * 7)); // Up to a week later

                var reply = new PostDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    DiscussionId = discussion.Id,
                    Content = postContents[_random.Next(postContents.Length)],
                    CreatedByUserId = replyAuthor.Id,
                    CreatedAt = replyCreatedAt,
                    IsFirstPost = false,
                    RevisionCount = 0
                };
                _context.Posts.Add(reply);

                if (replyCreatedAt > lastActivityAt)
                    lastActivityAt = replyCreatedAt;
            }

            discussion.LastActivityAt = lastActivityAt;
            discussion.PostCount = 1 + replyCount; // First post + replies
            discussion.ReactionCount = 0; // No reactions initially
            await _context.SaveChangesAsync();
        }
    }

    private int GetSkewedReplyCount()
    {
        // Simulate realistic reply distribution:
        // ~30% have 0-2 replies (low engagement)
        // ~40% have 3-7 replies (moderate)
        // ~20% have 8-15 replies (active)
        // ~10% have 16-30 replies (very active / viral)

        var roll = _random.Next(100);
        return roll switch
        {
            < 30 => _random.Next(0, 3),      // 0-2
            < 70 => _random.Next(3, 8),      // 3-7
            < 90 => _random.Next(8, 16),     // 8-15
            _ => _random.Next(16, 31)        // 16-30
        };
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("?", "")
            .Replace("!", "")
            .Replace(":", "")
            .Replace(",", "")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("(", "")
            .Replace(")", "");

        // Add short random suffix for uniqueness
        slug += "-" + Guid.NewGuid().ToString("N")[..6];
        return slug;
    }
}
