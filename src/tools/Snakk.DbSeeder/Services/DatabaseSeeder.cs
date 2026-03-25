namespace Snakk.DbSeeder.Services;

using Bogus;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class DatabaseSeeder(
    SnakkDbContext context,
    IPasswordHasher passwordHasher,
    IAvatarGenerationService avatarService,
    IMarkupParser markupParser,
    Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    private readonly SnakkDbContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IAvatarGenerationService _avatarService = avatarService;
    private readonly IMarkupParser _markupParser = markupParser;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration = configuration;

    // Fixed seed for reproducibility
    private const int Seed = 42;
    private readonly Faker _faker = new Faker("en") { Random = new Randomizer(Seed) };

    // Timeline: all seeded content within the last ~2 months
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateTime EarliestDate = Now.AddDays(-70);

    /// <summary>
    /// Run migrations only + ensure admin exists. No test data.
    /// </summary>
    public async Task SetupOnlyAsync()
    {
        await EnsureSystemSettingsAsync();
        await EnsureDefaultAdminExistsAsync();
        await GenerateAllAvatarsAsync();
    }

    private async Task EnsureSystemSettingsAsync()
    {
        var timezone = _configuration["Snakk:SiteTimezone"] ?? "UTC";
        await UpsertSystemSettingAsync("General", "Timezone", timezone, "String");
    }

    private async Task UpsertSystemSettingAsync(string category, string key, string value, string valueType)
    {
        var serialized = $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        var existing = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);

        if (existing is null)
        {
            _context.SystemSettings.Add(new SystemSettingDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Category = category,
                Key = key,
                Value = serialized,
                ValueType = valueType,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = serialized;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Full seed: admin + test users + communities + discussions.
    /// </summary>
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
        var test4Community = await CreateTest4CommunityAsync(users);
        var test5Community = await CreateTest5CommunityAsync(users);

        // Seed announcements across different scopes
        await SeedAnnouncementsAsync(snakkCommunity, users);

        // Seed moderators, bans, and custom report reasons
        await SeedModerationDataAsync(users);

        // Seed reactions (one per user per post)
        await SeedReactionsAsync(users);

        // Seed rules across all entity scopes
        await SeedRulesAsync();

        // Seed follows (users following discussions, spaces, and other users)
        await SeedFollowsAsync(users);

        // Seed reports (content reports with mod comments)
        await SeedReportsAsync(users);

        // Seed post revisions (edit history for some posts)
        await SeedPostRevisionsAsync(users);

        // Seed high-volume threads for pagination debugging
        await SeedHighVolumeDiscussionsAsync(users);

        // Seed community groups, member assignments, and group access control
        await SeedGroupsAndAccessAsync(snakkCommunity, users);

        // Recompute denormalized counts on Space, Hub, and Community entities
        await UpdateDenormalizedCountsAsync();

        Console.WriteLine("Database seeding completed successfully.");

        // Separate avatar generation phase
        await GenerateAllAvatarsAsync();
    }

    private async Task ClearExistingDataAsync()
    {
        // Delete in correct order due to foreign keys
        _context.GroupAccess.RemoveRange(_context.GroupAccess);
        _context.GroupMembers.RemoveRange(_context.GroupMembers);
        _context.Groups.RemoveRange(_context.Groups);
        _context.Rules.RemoveRange(_context.Rules);
        _context.Follows.RemoveRange(_context.Follows);
        _context.ReportComments.RemoveRange(_context.ReportComments);
        _context.Reports.RemoveRange(_context.Reports);
        _context.PostRevisions.RemoveRange(_context.PostRevisions);
        _context.Reactions.RemoveRange(_context.Reactions);
        _context.UserBans.RemoveRange(_context.UserBans);
        _context.ReportReasons.RemoveRange(_context.ReportReasons);
        _context.Announcements.RemoveRange(_context.Announcements);
        _context.Posts.RemoveRange(_context.Posts);
        _context.Discussions.RemoveRange(_context.Discussions);
        _context.Spaces.RemoveRange(_context.Spaces);
        _context.Hubs.RemoveRange(_context.Hubs);
        _context.CommunityDomains.RemoveRange(_context.CommunityDomains);
        _context.Communities.RemoveRange(_context.Communities);

        // Keep the test user and admin user, delete others
        var userIdsToKeep = new[] { "01JJQP0000000000000000TEST", "01JJQP0000000000000ADMIN" };
        var usersToDelete = _context.Users.Where(u => !userIdsToKeep.Contains(u.PublicId)).ToList();

        // Delete UserRole records for users being deleted (to avoid FK constraint violation)
        var userIdsToDelete = usersToDelete.Select(u => u.Id).ToList();
        var userRolesToDelete = _context.UserRoles.Where(ur => userIdsToDelete.Contains(ur.UserId));
        _context.UserRoles.RemoveRange(userRolesToDelete);

        // Now safe to delete users
        _context.Users.RemoveRange(usersToDelete);
        await _context.SaveChangesAsync();
        Console.WriteLine("Existing data cleared.");
    }

    /// <summary>
    /// Dedicated avatar generation phase. Scans the DB for all entities and generates
    /// avatars for any that don't already have one on disk.
    /// </summary>
    public async Task GenerateAllAvatarsAsync()
    {
        Console.WriteLine("Generating avatars for all entities...");

        // Users
        var userPublicIds = await _context.Users.Select(u => u.PublicId).ToListAsync();
        Console.WriteLine($"Generating avatars for {userPublicIds.Count} users...");
        for (var i = 0; i < userPublicIds.Count; i++)
        {
            await _avatarService.GenerateUserAvatarAsync(userPublicIds[i]);
            if ((i + 1) % 25 == 0 || i + 1 == userPublicIds.Count)
                Console.WriteLine($"  User avatars: {i + 1}/{userPublicIds.Count}");
        }

        // Communities
        var communityPublicIds = await _context.Communities.Select(c => c.PublicId).ToListAsync();
        Console.WriteLine($"Generating avatars for {communityPublicIds.Count} communities...");
        foreach (var id in communityPublicIds)
            await _avatarService.GenerateCommunityAvatarAsync(id);

        // Hubs
        var hubPublicIds = await _context.Hubs.Select(h => h.PublicId).ToListAsync();
        Console.WriteLine($"Generating avatars for {hubPublicIds.Count} hubs...");
        foreach (var id in hubPublicIds)
            await _avatarService.GenerateHubAvatarAsync(id);

        // Spaces
        var spacePublicIds = await _context.Spaces.Select(s => s.PublicId).ToListAsync();
        Console.WriteLine($"Generating avatars for {spacePublicIds.Count} spaces...");
        foreach (var id in spacePublicIds)
            await _avatarService.GenerateSpaceAvatarAsync(id);

        var total = userPublicIds.Count + communityPublicIds.Count + hubPublicIds.Count + spacePublicIds.Count;
        Console.WriteLine($"Avatar generation complete. {total} avatars generated.");
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
            CreatedAt = EarliestDate.AddDays(-30),
            LastSeenAt = Now
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();
        Console.WriteLine("Test user created.");
    }

    private async Task EnsureDefaultAdminExistsAsync()
    {
        // Read admin credentials from config (setup wizard writes these), fall back to defaults
        var adminEmail = _configuration["Setup:AdminEmail"] ?? "admin@snakk.local";
        var adminPassword = _configuration["Setup:AdminPassword"] ?? "admin123";
        var adminDisplayName = _configuration["Setup:AdminDisplayName"] ?? "Admin User";
        const string adminPublicId = "01JJQP0000000000000ADMIN";

        // Check if admin user already exists
        var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail || u.PublicId == adminPublicId);
        if (adminUser is not null)
        {
            // Ensure they have GlobalAdmin role in UserRoles table (for permissions)
            var hasGlobalAdminRole = await _context.UserRoles
                .AnyAsync(r => r.UserId == adminUser.Id && r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin && r.RevokedAt == null);

            if (!hasGlobalAdminRole)
            {
                _context.UserRoles.Add(new UserRoleDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = adminUser.Id,
                    RoleId = (int)UserRoleTypeEnum.GlobalAdmin,
                    AssignedByUserId = adminUser.Id, // Self-assigned
                    AssignedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                Console.WriteLine($"GlobalAdmin role assigned to existing user: {adminEmail}");
            }
            return;
        }

        var passwordHash = _passwordHasher.HashPassword(adminPassword);

        // Create new admin user
        var newAdminUser = new UserDatabaseEntity
        {
            PublicId = adminPublicId,
            Email = adminEmail,
            PasswordHash = passwordHash,
            DisplayName = adminDisplayName,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        _context.Users.Add(newAdminUser);
        await _context.SaveChangesAsync();

        // Assign GlobalAdmin role
        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = newAdminUser.Id,
            RoleId = (int)UserRoleTypeEnum.GlobalAdmin,
            AssignedByUserId = newAdminUser.Id, // Self-assigned
            AssignedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        Console.WriteLine($"Admin user created: {adminEmail}");
    }

    private async Task<List<UserDatabaseEntity>> SeedUsersAsync()
    {
        var users = new List<UserDatabaseEntity>();

        // Get the test user
        var testUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == "01JJQP0000000000000000TEST");
        if (testUser is not null)
            users.Add(testUser);

        // Get the admin user (created by EnsureDefaultAdminExistsAsync)
        var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == "01JJQP0000000000000ADMIN");
        if (adminUser is not null)
            users.Add(adminUser);

        // Generate 150 users with Bogus
        var userFaker = new Faker<UserDatabaseEntity>("en")
            .UseSeed(Seed)
            .RuleFor(u => u.PublicId, _ => Ulid.NewUlid().ToString())
            .RuleFor(u => u.DisplayName, f => f.Name.FullName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.DisplayName.Split(' ')[0], u.DisplayName.Split(' ').Last()).ToLower())
            .RuleFor(u => u.CreatedAt, f => f.Date.Between(EarliestDate.AddDays(-30), Now.AddDays(-7)))
            .RuleFor(u => u.LastSeenAt, f => f.Date.Between(Now.AddDays(-14), Now));

        var generatedUsers = userFaker.Generate(150);
        foreach (var user in generatedUsers)
        {
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
        var communityCreatedAt = EarliestDate;
        var community = new CommunityDatabaseEntity
        {
            PublicId = "01JJQP0000000000000SNAKK",
            Name = "Snakk",
            Slug = "snakk",
            Description = "The main Snakk community - discuss everything!",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = communityCreatedAt
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Hub 1: Technology (large - 5 spaces, heavily used)
        var techHub = await CreateHubAsync(community, "Technology", "technology", "All things tech", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techHub, "Web Development", "web-dev", "Frontend, backend, full-stack"), users, 220);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techHub, "Mobile Apps", "mobile", "iOS, Android, cross-platform"), users, 85);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techHub, "AI & Machine Learning", "ai-ml", "Neural networks, LLMs, data science"), users, 310);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techHub, "DevOps & Cloud", "devops", "AWS, Azure, Kubernetes, CI/CD"), users, 55);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techHub, "Programming Languages", "languages", "Rust, Go, Python, TypeScript and more"), users, 130);

        // Hub 2: Gaming (medium - 4 spaces)
        var gamingHub = await CreateHubAsync(community, "Gaming", "gaming", "Video games and esports", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(gamingHub, "PC Gaming", "pc", "Steam, Epic, GOG discussions"), users, 150);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(gamingHub, "Console Gaming", "console", "PlayStation, Xbox, Nintendo"), users, 110);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(gamingHub, "Indie Games", "indie", "Hidden gems and indie devs"), users, 40);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(gamingHub, "Esports", "esports", "Competitive gaming and tournaments"), users, 65);

        // Hub 3: Science (medium - 4 spaces)
        var scienceHub = await CreateHubAsync(community, "Science", "science", "Scientific discussions", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(scienceHub, "Physics", "physics", "Quantum to cosmos"), users, 70);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(scienceHub, "Biology", "biology", "Life sciences"), users, 45);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(scienceHub, "Space & Astronomy", "space", "The universe and beyond"), users, 90);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(scienceHub, "Climate & Environment", "climate", "Environmental science and sustainability"), users, 35);

        // Hub 4: Entertainment (small - 3 spaces)
        var entertainmentHub = await CreateHubAsync(community, "Entertainment", "entertainment", "Movies, TV, music, and more", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(entertainmentHub, "Movies & TV", "movies-tv", "What are you watching?"), users, 95);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(entertainmentHub, "Music", "music", "Genres, artists, playlists"), users, 60);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(entertainmentHub, "Books & Literature", "books", "Reading recommendations"), users, 50);

        Console.WriteLine("Created Snakk community with 4 hubs, 16 spaces.");
        return community;
    }

    // ===== TEST1 COMMUNITY (Small) =====
    private async Task<CommunityDatabaseEntity> CreateTest1CommunityAsync(List<UserDatabaseEntity> users)
    {
        var communityCreatedAt = EarliestDate.AddDays(5);
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Test Community One",
            Slug = "test1",
            Description = "A small test community for custom domain testing",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = communityCreatedAt
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Add custom domain
        _context.CommunityDomains.Add(new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "test1.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = communityCreatedAt
        });
        await _context.SaveChangesAsync();

        // Single hub with 3 spaces
        var generalHub = await CreateHubAsync(community, "General", "general", "General discussions", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(generalHub, "Announcements", "announcements", "Official announcements"), users, 15);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(generalHub, "Feedback", "feedback", "Share your feedback"), users, 55);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(generalHub, "Off-Topic", "off-topic", "Anything goes"), users, 30);

        Console.WriteLine("Created Test1 community (small) with custom domain test1.snakk.local");
        return community;
    }

    // ===== TEST2 COMMUNITY (Medium) =====
    private async Task<CommunityDatabaseEntity> CreateTest2CommunityAsync(List<UserDatabaseEntity> users)
    {
        var communityCreatedAt = EarliestDate.AddDays(3);
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Test Community Two",
            Slug = "test2",
            Description = "A medium-sized test community for development",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = communityCreatedAt
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Add custom domain
        _context.CommunityDomains.Add(new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "test2.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = communityCreatedAt
        });
        await _context.SaveChangesAsync();

        // Hub 1: Discussion
        var discussionHub = await CreateHubAsync(community, "Discussion", "discussion", "Open discussions", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(discussionHub, "Introductions", "intro", "Say hello!"), users, 110);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(discussionHub, "General Chat", "chat", "Off-topic conversations"), users, 180);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(discussionHub, "Q&A", "questions", "Ask anything"), users, 75);

        // Hub 2: Projects
        var projectsHub = await CreateHubAsync(community, "Projects", "projects", "Show off your work", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(projectsHub, "Showcase", "showcase", "Share your projects"), users, 45);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(projectsHub, "Collaboration", "collab", "Find collaborators"), users, 20);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(projectsHub, "Code Review", "code-review", "Get feedback on your code"), users, 35);

        Console.WriteLine("Created Test2 community (medium) with custom domain test2.snakk.local");
        return community;
    }

    // ===== TEST3 COMMUNITY (Large) =====
    private async Task<CommunityDatabaseEntity> CreateTest3CommunityAsync(List<UserDatabaseEntity> users)
    {
        var communityCreatedAt = EarliestDate.AddDays(1);
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Test Community Three",
            Slug = "test3",
            Description = "A large test community with lots of content",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = communityCreatedAt
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        // Add custom domain
        _context.CommunityDomains.Add(new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "test3.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = communityCreatedAt
        });
        await _context.SaveChangesAsync();

        // Hub 1: Learning (5 spaces)
        var learningHub = await CreateHubAsync(community, "Learning", "learning", "Educational content", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(learningHub, "Tutorials", "tutorials", "Step-by-step guides"), users, 250);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(learningHub, "Courses", "courses", "Recommended courses"), users, 95);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(learningHub, "Books", "books", "Book recommendations"), users, 130);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(learningHub, "Resources", "resources", "Useful links and tools"), users, 200);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(learningHub, "Study Groups", "study-groups", "Find study partners"), users, 40);

        // Hub 2: Community (4 spaces)
        var communityHub = await CreateHubAsync(community, "Community", "community", "Community matters", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(communityHub, "Events", "events", "Upcoming events"), users, 30);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(communityHub, "Meta", "meta", "Discussions about the community"), users, 70);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(communityHub, "Help Desk", "help", "Get help from the community"), users, 160);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(communityHub, "Suggestions", "suggestions", "Feature requests and ideas"), users, 50);

        // Hub 3: Creative (5 spaces - largest hub)
        var creativeHub = await CreateHubAsync(community, "Creative", "creative", "Creative works", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(creativeHub, "Writing", "writing", "Stories, poems, essays"), users, 140);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(creativeHub, "Art", "art", "Visual art and design"), users, 300);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(creativeHub, "Music", "music", "Music creation and appreciation"), users, 85);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(creativeHub, "Photography", "photo", "Photo sharing and critique"), users, 210);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(creativeHub, "Video", "video", "Video production"), users, 55);

        Console.WriteLine("Created Test3 community (large) with custom domain test3.snakk.local");
        return community;
    }

    // ===== TEST4 COMMUNITY (Niche) =====
    private async Task<CommunityDatabaseEntity> CreateTest4CommunityAsync(List<UserDatabaseEntity> users)
    {
        var communityCreatedAt = EarliestDate.AddDays(10);
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Cooking & Recipes",
            Slug = "cooking",
            Description = "A community for food lovers, home cooks, and chefs",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = communityCreatedAt
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        _context.CommunityDomains.Add(new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "cooking.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = communityCreatedAt
        });
        await _context.SaveChangesAsync();

        // Hub 1: Cuisine Types
        var cuisineHub = await CreateHubAsync(community, "Cuisines", "cuisines", "Explore world cuisines", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(cuisineHub, "Italian", "italian", "Pasta, pizza, and beyond"), users, 65);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(cuisineHub, "Asian", "asian", "Chinese, Japanese, Korean, Thai"), users, 80);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(cuisineHub, "Mexican", "mexican", "Tacos, tamales, and more"), users, 40);

        // Hub 2: Techniques
        var techniquesHub = await CreateHubAsync(community, "Techniques", "techniques", "Cooking methods and skills", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techniquesHub, "Baking", "baking", "Bread, pastries, cakes"), users, 90);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(techniquesHub, "Grilling & BBQ", "grilling", "Outdoor cooking"), users, 50);

        Console.WriteLine("Created Test4 community (niche/cooking) with custom domain cooking.snakk.local");
        return community;
    }

    // ===== TEST5 COMMUNITY (Fitness) =====
    private async Task<CommunityDatabaseEntity> CreateTest5CommunityAsync(List<UserDatabaseEntity> users)
    {
        var communityCreatedAt = EarliestDate.AddDays(8);
        var community = new CommunityDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Name = "Fitness & Health",
            Slug = "fitness",
            Description = "Workouts, nutrition, and healthy living",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = communityCreatedAt
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        _context.CommunityDomains.Add(new CommunityDomainDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Domain = "fitness.snakk.local",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = communityCreatedAt
        });
        await _context.SaveChangesAsync();

        // Hub 1: Workouts
        var workoutsHub = await CreateHubAsync(community, "Workouts", "workouts", "Exercise routines and programs", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(workoutsHub, "Strength Training", "strength", "Lifting and resistance training"), users, 75);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(workoutsHub, "Cardio", "cardio", "Running, cycling, swimming"), users, 55);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(workoutsHub, "Yoga & Flexibility", "yoga", "Mind-body practices"), users, 35);

        // Hub 2: Nutrition
        var nutritionHub = await CreateHubAsync(community, "Nutrition", "nutrition", "Diet and meal planning", communityCreatedAt);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(nutritionHub, "Meal Prep", "meal-prep", "Planning and prepping meals"), users, 60);
        await CreateDiscussionsForSpace(await CreateSpaceAsync(nutritionHub, "Supplements", "supplements", "Vitamins, protein, etc."), users, 40);

        Console.WriteLine("Created Test5 community (fitness) with custom domain fitness.snakk.local");
        return community;
    }

    // ===== HELPER METHODS =====

    private async Task<HubDatabaseEntity> CreateHubAsync(
        CommunityDatabaseEntity community, string name, string slug, string description, DateTime communityCreatedAt)
    {
        var hub = new HubDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Name = name,
            Slug = slug,
            Description = description,
            CreatedAt = communityCreatedAt.AddDays(_faker.Random.Int(1, 5))
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
            CreatedAt = hub.CreatedAt.AddDays(_faker.Random.Int(1, 3))
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        return space;
    }

    private async Task CreateDiscussionsForSpace(
        SpaceDatabaseEntity space, List<UserDatabaseEntity> users, int count)
    {
        Console.WriteLine($"  Creating {count} discussions in {space.Name}...");

        // Time window: from space creation to 1 hour ago (guaranteed past)
        var spaceCreated = space.CreatedAt;
        var latestAllowed = Now.AddHours(-1);

        // If space was created after our latest allowed, clamp
        if (spaceCreated >= latestAllowed)
            spaceCreated = latestAllowed.AddDays(-7);

        var totalMinutes = (latestAllowed - spaceCreated).TotalMinutes;

        // Batch for performance
        var discussions = new List<DiscussionDatabaseEntity>();
        var posts = new List<PostDatabaseEntity>();

        for (var i = 0; i < count; i++)
        {
            // Spread discussions across the time window with slight clustering toward recent
            var minutesOffset = _faker.Random.Double(0, totalMinutes);
            // Bias toward more recent: square root distribution pushes dates forward
            minutesOffset = totalMinutes * (1.0 - Math.Pow(1.0 - (minutesOffset / totalMinutes), 1.5));
            var createdAt = spaceCreated.AddMinutes(minutesOffset);

            var author = _faker.PickRandom(users);
            var title = GenerateDiscussionTitle(space.Name);
            var slug = GenerateSlug(title);
            var isPinned = _faker.Random.Int(1, 100) <= 3;  // 3% pinned
            var isLocked = _faker.Random.Int(1, 100) <= 1;  // 1% locked

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
            discussions.Add(discussion);
        }

        await _context.SaveChangesAsync();

        // Track which users have posted in this space (for IsUsersFirstPostInSpace)
        var usersWhoPostedInSpace = new HashSet<int>();
        var milestoneThresholds = new HashSet<int> { 100, 500, 1000, 2500, 5000, 10000 };
        var necroDays = 30;

        // Now create posts for each discussion
        foreach (var discussion in discussions)
        {
            var author = users.First(u => u.Id == discussion.CreatedByUserId);
            var usersWhoPostedInDiscussion = new HashSet<int>();
            var postNumber = 0;

            // First post (opening post) — usually longer
            postNumber++;
            var isFirstInSpace = usersWhoPostedInSpace.Add(author.Id);
            usersWhoPostedInDiscussion.Add(author.Id);
            var firstPostContent = GeneratePostContent(isOpeningPost: true);
            posts.Add(new PostDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                DiscussionId = discussion.Id,
                Content = firstPostContent,
                RenderedContent = _markupParser.ToHtml(firstPostContent),
                CreatedByUserId = author.Id,
                CreatedAt = discussion.CreatedAt,
                IsFirstPost = true,
                RevisionCount = 0,
                IsOp = true,
                IsUsersFirstPostInDiscussion = true,
                IsUsersFirstPostInSpace = isFirstInSpace,
                IsNecro = false,
                IsMilestone = milestoneThresholds.Contains(postNumber)
            });

            // Variable number of replies
            var replyCount = GetSkewedReplyCount();
            var lastActivityAt = discussion.CreatedAt;

            // Time budget: from discussion creation to latest allowed
            var replyTimeWindow = (latestAllowed - discussion.CreatedAt).TotalMinutes;

            for (var j = 0; j < replyCount; j++)
            {
                var replyAuthor = _faker.PickRandom(users);
                postNumber++;

                // Each reply is some time after the last, but capped to not exceed Now
                var maxDelay = Math.Max(5, replyTimeWindow / (replyCount + 1));
                var delay = _faker.Random.Double(5, Math.Min(maxDelay, 60 * 24 * 3)); // Up to 3 days, capped
                var replyCreatedAt = lastActivityAt.AddMinutes(delay);

                // Hard cap: never exceed 1 hour ago, but always after the discussion's first post
                if (replyCreatedAt >= latestAllowed)
                {
                    var minutesAfterDiscussion = (latestAllowed - discussion.CreatedAt).TotalMinutes;
                    var clampedDelay = minutesAfterDiscussion > 1
                        ? _faker.Random.Double(1, minutesAfterDiscussion)
                        : 1;
                    replyCreatedAt = discussion.CreatedAt.AddMinutes(clampedDelay);
                }

                var isFirstInDiscussion = usersWhoPostedInDiscussion.Add(replyAuthor.Id);
                var isFirstInSpaceReply = usersWhoPostedInSpace.Add(replyAuthor.Id);
                var isNecro = (replyCreatedAt - lastActivityAt).TotalDays >= necroDays;

                var replyContent = GeneratePostContent(isOpeningPost: false);
                posts.Add(new PostDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    DiscussionId = discussion.Id,
                    Content = replyContent,
                    RenderedContent = _markupParser.ToHtml(replyContent),
                    CreatedByUserId = replyAuthor.Id,
                    CreatedAt = replyCreatedAt,
                    IsFirstPost = false,
                    RevisionCount = 0,
                    IsOp = replyAuthor.Id == discussion.CreatedByUserId,
                    IsUsersFirstPostInDiscussion = isFirstInDiscussion,
                    IsUsersFirstPostInSpace = isFirstInSpaceReply,
                    IsNecro = isNecro,
                    IsMilestone = milestoneThresholds.Contains(postNumber)
                });

                if (replyCreatedAt > lastActivityAt)
                    lastActivityAt = replyCreatedAt;
            }

            discussion.LastActivityAt = lastActivityAt;
            discussion.PostCount = 1 + replyCount;
            discussion.ReactionCount = 0;
        }

        _context.Posts.AddRange(posts);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Generate a realistic discussion title using Bogus.
    /// Mixes template-based titles with generated sentences.
    /// </summary>
    private string GenerateDiscussionTitle(string spaceName)
    {
        var roll = _faker.Random.Int(1, 100);

        if (roll <= 40)
        {
            // Template-based titles (40%)
            var templates = new[]
            {
                "Getting started with {0}",
                "Best practices for {0}",
                "Common mistakes in {0}",
                "Advanced {0} techniques",
                "What's new in {0}?",
                "Tips and tricks for {0}",
                "Troubleshooting {0} issues",
                "My experience with {0}",
                "Question about {0}",
                "How to improve at {0}",
                "The future of {0}",
                "Share your {0} work",
                "Help needed with {0}",
                "{0} for beginners",
                "Comparing {0} approaches",
                "Weekly {0} thread",
                "Thoughts on {0}?",
                "{0} recommendations",
                "Learning {0} — where to start?",
                "Why I love {0}",
                "{0} challenges this week",
                "Beginner's guide to {0}",
                "{0} inspiration thread",
                "Unpopular opinion about {0}",
                "What got you into {0}?",
                "{0} resources roundup",
                "Is {0} still worth it?",
                "Hot take on {0}",
                "My {0} journey so far",
                "Can we talk about {0}?"
            };
            return string.Format(_faker.PickRandom(templates), spaceName);
        }

        if (roll <= 70)
        {
            // Question-style titles (30%)
            var questionStarters = new[]
            {
                "How do you", "What's the best way to", "Has anyone tried",
                "Why does", "Can someone explain", "What do you think about",
                "Should I", "Is it possible to", "Does anyone know",
                "How long does it take to", "What's your favorite",
                "Am I the only one who", "Who else struggles with"
            };
            return $"{_faker.PickRandom(questionStarters)} {_faker.Lorem.Sentence(3, 5).TrimEnd('.')}?";
        }

        if (roll <= 90)
        {
            // Statement titles (20%)
            return _faker.Lorem.Sentence(4, 8).TrimEnd('.') + _faker.PickRandom("", "!", " — my thoughts", " [Discussion]", " (updated)");
        }

        // Rant-style titles (10%)
        return _faker.Rant.Review(spaceName);
    }

    /// <summary>
    /// Generate post content with natural size distribution.
    /// Opening posts tend to be longer, replies vary from one-liners to detailed responses.
    /// </summary>
    private string GeneratePostContent(bool isOpeningPost)
    {
        var roll = _faker.Random.Int(1, 100);

        if (isOpeningPost)
        {
            // Opening posts are generally longer
            if (roll <= 15)
            {
                // Short opener (15%): 1-2 sentences
                return _faker.Lorem.Sentences(_faker.Random.Int(1, 2));
            }
            if (roll <= 45)
            {
                // Medium opener (30%): 1-2 paragraphs
                return _faker.Lorem.Paragraphs(_faker.Random.Int(1, 2), "\n\n");
            }
            if (roll <= 75)
            {
                // Long opener (30%): 3-5 paragraphs
                return _faker.Lorem.Paragraphs(_faker.Random.Int(3, 5), "\n\n");
            }
            if (roll <= 90)
            {
                // Very long opener (15%): 6-8 paragraphs — detailed writeup
                return _faker.Lorem.Paragraphs(_faker.Random.Int(6, 8), "\n\n");
            }
            // Epic opener (10%): 8-12 paragraphs — essay-length
            return _faker.Lorem.Paragraphs(_faker.Random.Int(8, 12), "\n\n");
        }

        // Replies
        if (roll <= 40)
        {
            // Quick reply (40%): 1-2 sentences
            return _faker.Lorem.Sentences(_faker.Random.Int(1, 2));
        }
        if (roll <= 65)
        {
            // Medium reply (25%): 3-5 sentences
            return _faker.Lorem.Sentences(_faker.Random.Int(3, 5));
        }
        if (roll <= 80)
        {
            // Thoughtful reply (15%): 1-2 paragraphs
            return _faker.Lorem.Paragraphs(_faker.Random.Int(1, 2), "\n\n");
        }
        if (roll <= 92)
        {
            // Detailed reply (12%): 3-5 paragraphs
            return _faker.Lorem.Paragraphs(_faker.Random.Int(3, 5), "\n\n");
        }
        // Long reply (8%): 5-10 paragraphs — someone really got into it
        return _faker.Lorem.Paragraphs(_faker.Random.Int(5, 10), "\n\n");
    }

    private async Task SeedHighVolumeDiscussionsAsync(List<UserDatabaseEntity> users)
    {
        Console.WriteLine("Seeding high-volume discussions for pagination debugging...");

        // Find the Web Development space in the Snakk community
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.Slug == "web-dev");
        if (space is null)
        {
            Console.WriteLine("  Could not find web-dev space, skipping high-volume seed.");
            return;
        }

        var latestAllowed = Now.AddHours(-1);
        var milestoneThresholds = new HashSet<int> { 100, 500, 1000, 2500, 5000, 10000 };
        var usersWhoPostedInSpace = new HashSet<int>();

        var threads = new[]
        {
            ("The Mega Thread: Everything Web Development 2025", 247),
            ("Ask Me Anything: Senior Dev Career Q&A", 183),
            ("Framework Wars: React vs Vue vs Svelte vs HTMX", 312),
        };

        foreach (var (title, postCount) in threads)
        {
            var author = _faker.PickRandom(users);
            var slug = GenerateSlug(title);
            var createdAt = Now.AddHours(-6); // Recent so it's easy to find

            var discussion = new DiscussionDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                SpaceId = space.Id,
                Title = title,
                Slug = slug,
                CreatedByUserId = author.Id,
                CreatedAt = createdAt,
                LastActivityAt = createdAt,
                IsPinned = true
            };
            _context.Discussions.Add(discussion);
            await _context.SaveChangesAsync();

            var posts = new List<PostDatabaseEntity>();
            var lastActivityAt = createdAt;
            var replyTimeWindow = (latestAllowed - createdAt).TotalMinutes;
            var replyCount = postCount - 1; // first post counts as 1
            var usersWhoPostedInDiscussion = new HashSet<int>();
            var postNumber = 0;

            // First post
            postNumber++;
            usersWhoPostedInDiscussion.Add(author.Id);
            var isFirstInSpace = usersWhoPostedInSpace.Add(author.Id);
            var firstPostContent = GeneratePostContent(isOpeningPost: true);
            posts.Add(new PostDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                DiscussionId = discussion.Id,
                Content = firstPostContent,
                RenderedContent = _markupParser.ToHtml(firstPostContent),
                CreatedByUserId = author.Id,
                CreatedAt = createdAt,
                IsFirstPost = true,
                RevisionCount = 0,
                IsOp = true,
                IsUsersFirstPostInDiscussion = true,
                IsUsersFirstPostInSpace = isFirstInSpace,
                IsNecro = false,
                IsMilestone = false
            });

            for (var j = 0; j < replyCount; j++)
            {
                var replyAuthor = _faker.PickRandom(users);
                postNumber++;
                var maxDelay = Math.Max(5, replyTimeWindow / (replyCount + 1));
                var delay = _faker.Random.Double(5, Math.Min(maxDelay, 60 * 12));
                var replyCreatedAt = lastActivityAt.AddMinutes(delay);

                if (replyCreatedAt >= latestAllowed)
                {
                    var minutesAfterDiscussion = (latestAllowed - createdAt).TotalMinutes;
                    replyCreatedAt = createdAt.AddMinutes(minutesAfterDiscussion > 1
                        ? _faker.Random.Double(1, minutesAfterDiscussion)
                        : 1);
                }

                var isFirstInDiscussion = usersWhoPostedInDiscussion.Add(replyAuthor.Id);
                var isFirstInSpaceReply = usersWhoPostedInSpace.Add(replyAuthor.Id);

                var replyContent = GeneratePostContent(isOpeningPost: false);
                posts.Add(new PostDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    DiscussionId = discussion.Id,
                    Content = replyContent,
                    RenderedContent = _markupParser.ToHtml(replyContent),
                    CreatedByUserId = replyAuthor.Id,
                    CreatedAt = replyCreatedAt,
                    IsFirstPost = false,
                    RevisionCount = 0,
                    IsOp = replyAuthor.Id == discussion.CreatedByUserId,
                    IsUsersFirstPostInDiscussion = isFirstInDiscussion,
                    IsUsersFirstPostInSpace = isFirstInSpaceReply,
                    IsNecro = false,
                    IsMilestone = milestoneThresholds.Contains(postNumber)
                });

                if (replyCreatedAt > lastActivityAt)
                    lastActivityAt = replyCreatedAt;
            }

            discussion.LastActivityAt = lastActivityAt;
            discussion.PostCount = postCount;
            discussion.ReactionCount = 0;

            _context.Posts.AddRange(posts);
            await _context.SaveChangesAsync();

            Console.WriteLine($"  Seeded \"{title}\" with {postCount} posts.");
        }

        // Seed necro discussions (threads with 30+ day gaps between posts)
        await SeedNecroDiscussionsAsync(space, users, usersWhoPostedInSpace);
    }

    private async Task SeedNecroDiscussionsAsync(SpaceDatabaseEntity space, List<UserDatabaseEntity> users, HashSet<int> usersWhoPostedInSpace)
    {
        Console.WriteLine("  Seeding necro discussions (30+ day gaps)...");

        var necroThreads = new[]
        {
            (Title: "Old Bug Report: Memory Leak in Production", GapDays: 45, RepliesBefore: 5, RepliesAfter: 3),
            (Title: "Anyone Still Using This Library?", GapDays: 120, RepliesBefore: 8, RepliesAfter: 2),
            (Title: "Throwback: The State of CSS in 2024", GapDays: 200, RepliesBefore: 12, RepliesAfter: 4),
        };

        foreach (var thread in necroThreads)
        {
            var author = _faker.PickRandom(users);
            var slug = GenerateSlug(thread.Title);
            var createdAt = Now.AddDays(-(thread.GapDays + 14)); // Start well before the gap

            var discussion = new DiscussionDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                SpaceId = space.Id,
                Title = thread.Title,
                Slug = slug,
                CreatedByUserId = author.Id,
                CreatedAt = createdAt,
                LastActivityAt = createdAt
            };
            _context.Discussions.Add(discussion);
            await _context.SaveChangesAsync();

            var posts = new List<PostDatabaseEntity>();
            var usersWhoPostedInDiscussion = new HashSet<int>();
            var postNumber = 0;
            var lastPostDate = createdAt;

            // First post (OP)
            postNumber++;
            usersWhoPostedInDiscussion.Add(author.Id);
            var isFirstInSpace = usersWhoPostedInSpace.Add(author.Id);
            var firstContent = GeneratePostContent(isOpeningPost: true);
            posts.Add(new PostDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                DiscussionId = discussion.Id,
                Content = firstContent,
                RenderedContent = _markupParser.ToHtml(firstContent),
                CreatedByUserId = author.Id,
                CreatedAt = createdAt,
                IsFirstPost = true,
                RevisionCount = 0,
                IsOp = true,
                IsUsersFirstPostInDiscussion = true,
                IsUsersFirstPostInSpace = isFirstInSpace,
                IsNecro = false,
                IsMilestone = false
            });
            lastPostDate = createdAt;

            // Replies BEFORE the gap (normal cadence, hours apart)
            for (var j = 0; j < thread.RepliesBefore; j++)
            {
                var replyAuthor = _faker.PickRandom(users);
                postNumber++;
                var delay = _faker.Random.Double(30, 60 * 24 * 2); // 30 min to 2 days
                var replyDate = lastPostDate.AddMinutes(delay);
                var isFirstInDiscussion = usersWhoPostedInDiscussion.Add(replyAuthor.Id);
                var isFirstInSpaceReply = usersWhoPostedInSpace.Add(replyAuthor.Id);

                var content = GeneratePostContent(isOpeningPost: false);
                posts.Add(new PostDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    DiscussionId = discussion.Id,
                    Content = content,
                    RenderedContent = _markupParser.ToHtml(content),
                    CreatedByUserId = replyAuthor.Id,
                    CreatedAt = replyDate,
                    IsFirstPost = false,
                    RevisionCount = 0,
                    IsOp = replyAuthor.Id == discussion.CreatedByUserId,
                    IsUsersFirstPostInDiscussion = isFirstInDiscussion,
                    IsUsersFirstPostInSpace = isFirstInSpaceReply,
                    IsNecro = false,
                    IsMilestone = false
                });
                lastPostDate = replyDate;
            }

            // THE NECRO GAP — jump forward by gapDays
            var necroDate = lastPostDate.AddDays(thread.GapDays);

            // Replies AFTER the gap (the necro revival)
            for (var j = 0; j < thread.RepliesAfter; j++)
            {
                var replyAuthor = _faker.PickRandom(users);
                postNumber++;
                var isNecro = j == 0; // Only the first post after the gap is necro
                var replyDate = j == 0 ? necroDate : necroDate.AddMinutes(_faker.Random.Double(10, 60 * 8));
                var isFirstInDiscussion = usersWhoPostedInDiscussion.Add(replyAuthor.Id);
                var isFirstInSpaceReply = usersWhoPostedInSpace.Add(replyAuthor.Id);

                var content = GeneratePostContent(isOpeningPost: false);
                posts.Add(new PostDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    DiscussionId = discussion.Id,
                    Content = content,
                    RenderedContent = _markupParser.ToHtml(content),
                    CreatedByUserId = replyAuthor.Id,
                    CreatedAt = replyDate,
                    IsFirstPost = false,
                    RevisionCount = 0,
                    IsOp = replyAuthor.Id == discussion.CreatedByUserId,
                    IsUsersFirstPostInDiscussion = isFirstInDiscussion,
                    IsUsersFirstPostInSpace = isFirstInSpaceReply,
                    IsNecro = isNecro,
                    IsMilestone = false
                });
                if (replyDate > necroDate) necroDate = replyDate;
            }

            discussion.LastActivityAt = necroDate;
            discussion.PostCount = postNumber;
            discussion.ReactionCount = 0;

            _context.Posts.AddRange(posts);
            await _context.SaveChangesAsync();

            Console.WriteLine($"  Seeded necro \"{thread.Title}\" ({thread.RepliesBefore} posts, {thread.GapDays}d gap, {thread.RepliesAfter} necro replies).");
        }
    }

    private int GetSkewedReplyCount()
    {
        // Simulate realistic reply distribution:
        // ~30% have 0-2 replies (low engagement)
        // ~40% have 3-7 replies (moderate)
        // ~20% have 8-15 replies (active)
        // ~10% have 16-30 replies (very active / viral)
        var roll = _faker.Random.Int(0, 99);
        return roll switch
        {
            < 30 => _faker.Random.Int(0, 2),
            < 70 => _faker.Random.Int(3, 7),
            < 90 => _faker.Random.Int(8, 15),
            _ => _faker.Random.Int(16, 30)
        };
    }

    private async Task SeedReactionsAsync(List<UserDatabaseEntity> users)
    {
        Console.WriteLine("Seeding reactions...");

        var allReactionTypes = Enum.GetValues<ReactionTypeEnum>();

        // Load all posts (id, discussion id, created at) in one query
        var posts = await _context.Posts
            .Select(p => new { p.Id, p.DiscussionId, p.CreatedAt })
            .ToListAsync();

        Console.WriteLine($"  Distributing reactions across {posts.Count} posts...");

        var reactions = new List<ReactionDatabaseEntity>();

        // Track reaction counts for denormalized updates: postId -> count, discussionId -> count
        var postReactionCounts = new Dictionary<int, int>();
        var discussionReactionCounts = new Dictionary<int, int>();

        foreach (var post in posts)
        {
            // Distribution: 70% no reactions, 20% 1-2, 7% 3-4, 3% exactly 5
            var roll = _faker.Random.Int(0, 99);
            var reactorCount = roll switch
            {
                < 70 => 0,
                < 90 => _faker.Random.Int(1, 2),
                < 97 => _faker.Random.Int(3, 4),
                _    => 5
            };

            if (reactorCount == 0) continue;

            reactorCount = Math.Min(reactorCount, users.Count);

            // Pick unique reactors (shuffle + take)
            var reactors = users
                .OrderBy(_ => _faker.Random.Int())
                .Take(reactorCount);

            foreach (var user in reactors)
            {
                reactions.Add(new ReactionDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    PostId = post.Id,
                    UserId = user.Id,
                    TypeId = (int)_faker.PickRandom(allReactionTypes),
                    CreatedAt = post.CreatedAt.AddMinutes(_faker.Random.Double(1, 60 * 24))
                });

                postReactionCounts[post.Id] = postReactionCounts.GetValueOrDefault(post.Id) + 1;
                discussionReactionCounts[post.DiscussionId] = discussionReactionCounts.GetValueOrDefault(post.DiscussionId) + 1;
            }
        }

        // Batch insert in chunks of 1000 to avoid memory issues
        const int chunkSize = 1000;
        for (var i = 0; i < reactions.Count; i += chunkSize)
        {
            _context.Reactions.AddRange(reactions.Skip(i).Take(chunkSize));
            await _context.SaveChangesAsync();
            Console.WriteLine($"  Inserted reactions {Math.Min(i + chunkSize, reactions.Count)}/{reactions.Count}");
        }

        // Update denormalized reaction counts on posts and discussions
        var postsToUpdate = await _context.Posts
            .Where(p => postReactionCounts.Keys.Contains(p.Id))
            .ToListAsync();

        foreach (var post in postsToUpdate)
            post.ReactionCount = postReactionCounts.GetValueOrDefault(post.Id);

        var discussionsToUpdate = await _context.Discussions
            .Where(d => discussionReactionCounts.Keys.Contains(d.Id))
            .ToListAsync();

        foreach (var discussion in discussionsToUpdate)
            discussion.ReactionCount = discussionReactionCounts.GetValueOrDefault(discussion.Id);

        await _context.SaveChangesAsync();

        Console.WriteLine($"Seeded {reactions.Count} reactions across {postReactionCounts.Count} posts.");
    }

    private async Task UpdateDenormalizedCountsAsync()
    {
        Console.WriteLine("Updating denormalized counts on spaces, hubs, and communities...");

        // Spaces: count discussions and posts per space
        var spaceDiscussionCounts = await _context.Discussions
            .GroupBy(d => d.SpaceId)
            .Select(g => new { SpaceId = g.Key, DiscussionCount = g.Count(), PostCount = g.Sum(d => d.PostCount) })
            .ToListAsync();

        var spaceReactionCounts = await _context.Discussions
            .GroupBy(d => d.SpaceId)
            .Select(g => new { SpaceId = g.Key, ReactionCount = g.Sum(d => d.ReactionCount) })
            .ToListAsync();

        var spaces = await _context.Spaces.ToListAsync();
        foreach (var space in spaces)
        {
            var disc = spaceDiscussionCounts.FirstOrDefault(x => x.SpaceId == space.Id);
            var react = spaceReactionCounts.FirstOrDefault(x => x.SpaceId == space.Id);
            space.DiscussionCount = disc?.DiscussionCount ?? 0;
            space.PostCount = disc?.PostCount ?? 0;
            space.ReactionCount = react?.ReactionCount ?? 0;
        }

        await _context.SaveChangesAsync();

        // Hubs: aggregate from spaces and discussions
        var hubSpaceCounts = await _context.Spaces
            .GroupBy(s => s.HubId)
            .Select(g => new { HubId = g.Key, SpaceCount = g.Count(), DiscussionCount = g.Sum(s => s.DiscussionCount), PostCount = g.Sum(s => s.PostCount), ReactionCount = g.Sum(s => s.ReactionCount) })
            .ToListAsync();

        var hubs = await _context.Hubs.ToListAsync();
        foreach (var hub in hubs)
        {
            var agg = hubSpaceCounts.FirstOrDefault(x => x.HubId == hub.Id);
            hub.SpaceCount = agg?.SpaceCount ?? 0;
            hub.DiscussionCount = agg?.DiscussionCount ?? 0;
            hub.PostCount = agg?.PostCount ?? 0;
            hub.ReactionCount = agg?.ReactionCount ?? 0;
        }

        await _context.SaveChangesAsync();

        // Communities: aggregate from hubs
        var communityHubCounts = await _context.Hubs
            .GroupBy(h => h.CommunityId)
            .Select(g => new { CommunityId = g.Key, HubCount = g.Count(), SpaceCount = g.Sum(h => h.SpaceCount), DiscussionCount = g.Sum(h => h.DiscussionCount), PostCount = g.Sum(h => h.PostCount), ReactionCount = g.Sum(h => h.ReactionCount) })
            .ToListAsync();

        var communities = await _context.Communities.ToListAsync();
        foreach (var community in communities)
        {
            var agg = communityHubCounts.FirstOrDefault(x => x.CommunityId == community.Id);
            community.HubCount = agg?.HubCount ?? 0;
            community.SpaceCount = agg?.SpaceCount ?? 0;
            community.DiscussionCount = agg?.DiscussionCount ?? 0;
            community.PostCount = agg?.PostCount ?? 0;
            community.ReactionCount = agg?.ReactionCount ?? 0;
        }

        await _context.SaveChangesAsync();

        Console.WriteLine($"Updated counts for {spaces.Count} spaces, {hubs.Count} hubs, {communities.Count} communities.");
    }

    private string GenerateSlug(string title)
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
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("—", "-")
            .Replace("--", "-")
            .Trim('-');

        // Truncate long slugs
        if (slug.Length > 80)
            slug = slug[..80].TrimEnd('-');

        return slug;
    }

    private async Task SeedAnnouncementsAsync(
        CommunityDatabaseEntity community,
        List<UserDatabaseEntity> users)
    {
        var adminUser = users.First(u => u.PublicId == "01JJQP0000000000000ADMIN");

        // Get the first hub and space for hub/space-level announcements
        var hub = await _context.Hubs
            .Where(h => h.CommunityId == community.Id)
            .FirstAsync();

        var space = await _context.Spaces
            .Where(s => s.HubId == hub.Id)
            .FirstAsync();

        // Community-level: Welcome announcement (Info, permanent)
        var welcomeContent = "Welcome to the community! Please read the rules and be respectful to other members.";
        _context.Announcements.Add(new AnnouncementDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Title = "Welcome to the community!",
            Content = welcomeContent,
            RenderedContent = _markupParser.ToHtml(welcomeContent),
            TypeId = (int)AnnouncementTypeEnum.Info,
            ScopeId = (int)AnnouncementScopeEnum.Community,
            ScopeEntityId = community.PublicId,
            IsDismissible = true,
            SortOrder = 0,
            CreatedByUserId = adminUser.Id,
            CreatedAt = EarliestDate.AddDays(1)
        });

        // Community-level: Maintenance warning (Warning, time-limited)
        var maintenanceContent = "**Scheduled maintenance** on Saturday at 02:00 UTC. The platform may be briefly unavailable.";
        _context.Announcements.Add(new AnnouncementDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Title = "Upcoming Maintenance",
            Content = maintenanceContent,
            RenderedContent = _markupParser.ToHtml(maintenanceContent),
            TypeId = (int)AnnouncementTypeEnum.Warning,
            ScopeId = (int)AnnouncementScopeEnum.Community,
            ScopeEntityId = community.PublicId,
            VisibleFrom = Now.AddDays(-1),
            VisibleUntil = Now.AddDays(7),
            IsDismissible = true,
            SortOrder = 1,
            CreatedByUserId = adminUser.Id,
            CreatedAt = Now.AddDays(-1)
        });

        // Hub-level: New rules announcement (Info)
        var rulesContent = "New community guidelines are now in effect for this hub. Please review the updated rules in the sidebar.";
        _context.Announcements.Add(new AnnouncementDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Title = "Updated Hub Guidelines",
            Content = rulesContent,
            RenderedContent = _markupParser.ToHtml(rulesContent),
            TypeId = (int)AnnouncementTypeEnum.Info,
            ScopeId = (int)AnnouncementScopeEnum.Hub,
            ScopeEntityId = hub.PublicId,
            IsDismissible = true,
            SortOrder = 0,
            CreatedByUserId = adminUser.Id,
            CreatedAt = EarliestDate.AddDays(10)
        });

        // Space-level: Under review (Critical, non-dismissible)
        var reviewContent = "This space is currently under moderation review. Some features may be temporarily restricted.";
        _context.Announcements.Add(new AnnouncementDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Title = "Space Under Review",
            Content = reviewContent,
            RenderedContent = _markupParser.ToHtml(reviewContent),
            TypeId = (int)AnnouncementTypeEnum.Critical,
            ScopeId = (int)AnnouncementScopeEnum.Space,
            ScopeEntityId = space.PublicId,
            IsDismissible = false,
            SortOrder = 0,
            CreatedByUserId = adminUser.Id,
            CreatedAt = Now.AddDays(-3)
        });

        await _context.SaveChangesAsync();
        Console.WriteLine("Created 4 seed announcements (2 community, 1 hub, 1 space).");
    }

    private async Task SeedRulesAsync()
    {
        Console.WriteLine("Seeding rules...");

        var rules = new List<RuleDatabaseEntity>();

        // Site-wide rules (no scope — all nulls)
        var siteRules = new[]
        {
            ("Be respectful", "Treat others with courtesy. No personal attacks, harassment, or hate speech of any kind."),
            ("No spam or self-promotion", "Do not post unsolicited advertisements, referral links, or repetitive self-promotional content."),
            ("Keep it legal", "Do not post content that violates applicable laws, including piracy, doxxing, or threats of violence.")
        };

        for (var i = 0; i < siteRules.Length; i++)
        {
            rules.Add(new RuleDatabaseEntity
            {
                Title = siteRules[i].Item1,
                Description = siteRules[i].Item2,
                SortOrder = i + 1,
                CreatedAt = EarliestDate
            });
        }

        // Community rules
        var communityRulePool = new[]
        {
            ("Use descriptive titles", "Discussion titles should clearly describe the topic. Avoid vague titles like \"Help\" or \"Question\"."),
            ("English only", "All posts and comments must be written in English to keep discussions accessible to everyone."),
            ("No NSFW content", "This is a safe-for-work community. Do not post explicit, graphic, or sexually suggestive content."),
            ("Cite your sources", "When making factual claims, provide links or references to credible sources."),
            ("Stay on topic", "Keep discussions relevant to the community's purpose. Off-topic posts may be moved or removed."),
            ("No trolling or bad faith", "Engage genuinely. Deliberately inflammatory or disingenuous posts will be removed."),
            ("Respect privacy", "Do not share personal information about others without their consent.")
        };

        var communities = await _context.Communities.ToListAsync();
        foreach (var community in communities)
        {
            var count = _faker.Random.Int(0, 3);
            var picked = _faker.PickRandom(communityRulePool, count).ToList();
            for (var i = 0; i < picked.Count; i++)
            {
                rules.Add(new RuleDatabaseEntity
                {
                    Title = picked[i].Item1,
                    Description = picked[i].Item2,
                    SortOrder = i + 1,
                    CommunityId = community.Id,
                    CreatedAt = community.CreatedAt.AddDays(1)
                });
            }
        }

        // Hub rules
        var hubRulePool = new[]
        {
            ("Use appropriate flair", "Tag your posts with the correct category or flair so others can filter content easily."),
            ("No duplicate threads", "Search before posting. If a similar discussion exists, contribute there instead of creating a new one."),
            ("Keep titles neutral", "Avoid editorializing or sensationalizing titles. Present the topic fairly."),
            ("Constructive feedback only", "Criticism is welcome, but it must be constructive and aimed at ideas, not people."),
            ("No low-effort posts", "One-word replies, memes without context, and low-effort content will be removed."),
            ("Spoiler tags required", "Use spoiler syntax when discussing plot points, leaks, or unreleased content.")
        };

        var hubs = await _context.Hubs.ToListAsync();
        foreach (var hub in hubs)
        {
            var count = _faker.Random.Int(0, 3);
            var picked = _faker.PickRandom(hubRulePool, count).ToList();
            for (var i = 0; i < picked.Count; i++)
            {
                rules.Add(new RuleDatabaseEntity
                {
                    Title = picked[i].Item1,
                    Description = picked[i].Item2,
                    SortOrder = i + 1,
                    HubId = hub.Id,
                    CreatedAt = hub.CreatedAt.AddDays(1)
                });
            }
        }

        // Space rules
        var spaceRulePool = new[]
        {
            ("Stay on topic for this space", "Posts must be directly related to this space's subject matter."),
            ("No homework requests", "Do not ask others to do your assignments. Show your own effort first."),
            ("Format code properly", "Use code blocks and syntax highlighting when sharing code snippets."),
            ("Beginner-friendly zone", "Be patient with newcomers. Everyone starts somewhere."),
            ("No buying or selling", "This is a discussion space, not a marketplace. No trade or sale posts."),
            ("Credit original creators", "When sharing someone else's work, always credit the original author or source."),
            ("Weekly threads only", "Recurring topics should go in the designated weekly thread, not as standalone posts.")
        };

        var spaces = await _context.Spaces.ToListAsync();
        foreach (var space in spaces)
        {
            var count = _faker.Random.Int(0, 3);
            var picked = _faker.PickRandom(spaceRulePool, count).ToList();
            for (var i = 0; i < picked.Count; i++)
            {
                rules.Add(new RuleDatabaseEntity
                {
                    Title = picked[i].Item1,
                    Description = picked[i].Item2,
                    SortOrder = i + 1,
                    SpaceId = space.Id,
                    CreatedAt = space.CreatedAt.AddDays(1)
                });
            }
        }

        _context.Rules.AddRange(rules);
        await _context.SaveChangesAsync();

        // Set denormalized HasRules / ParentHasRules flags
        var communityIdsWithRules = new HashSet<int>(rules.Where(r => r.CommunityId.HasValue).Select(r => r.CommunityId!.Value));
        var hubIdsWithRules = new HashSet<int>(rules.Where(r => r.HubId.HasValue).Select(r => r.HubId!.Value));
        var spaceIdsWithRules = new HashSet<int>(rules.Where(r => r.SpaceId.HasValue).Select(r => r.SpaceId!.Value));

        foreach (var community in communities)
        {
            community.HasRules = communityIdsWithRules.Contains(community.Id);
            if (community.HasRules)
                community.RulesRevision = Guid.NewGuid().ToString("N")[..8];
        }

        foreach (var hub in hubs)
        {
            hub.HasRules = hubIdsWithRules.Contains(hub.Id);
            hub.ParentCommunityHasRules = communityIdsWithRules.Contains(hub.CommunityId);
            if (hub.HasRules)
                hub.RulesRevision = Guid.NewGuid().ToString("N")[..8];
        }

        foreach (var space in spaces)
        {
            space.HasRules = spaceIdsWithRules.Contains(space.Id);
            space.ParentHubHasRules = hubIdsWithRules.Contains(space.HubId);
            var hub = hubs.First(h => h.Id == space.HubId);
            space.ParentCommunityHasRules = communityIdsWithRules.Contains(hub.CommunityId);
            if (space.HasRules)
                space.RulesRevision = Guid.NewGuid().ToString("N")[..8];
        }

        // Seed SiteRulesRevision system setting
        await UpsertSystemSettingAsync("Rules", "SiteRulesRevision", Guid.NewGuid().ToString("N")[..8], "String");

        await _context.SaveChangesAsync();

        var siteCount = siteRules.Length;
        var communityCount = rules.Count(r => r.CommunityId.HasValue);
        var hubCount = rules.Count(r => r.HubId.HasValue);
        var spaceCount = rules.Count(r => r.SpaceId.HasValue);
        Console.WriteLine($"Seeded {rules.Count} rules ({siteCount} site-wide, {communityCount} community, {hubCount} hub, {spaceCount} space).");
    }

    private async Task SeedFollowsAsync(List<UserDatabaseEntity> users)
    {
        Console.WriteLine("Seeding follows...");

        var follows = new List<FollowDatabaseEntity>();
        var discussions = await _context.Discussions.ToListAsync();
        var spaces = await _context.Spaces.ToListAsync();

        // Each user follows 0-5 discussions
        foreach (var user in users)
        {
            var discussionCount = _faker.Random.Int(0, 5);
            var pickedDiscussions = _faker.PickRandom(discussions, Math.Min(discussionCount, discussions.Count));
            foreach (var discussion in pickedDiscussions)
            {
                follows.Add(new FollowDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = user.Id,
                    TargetTypeId = (int)FollowTargetTypeEnum.Discussion,
                    LevelId = (int)FollowLevelEnum.DiscussionsAndPosts,
                    DiscussionId = discussion.Id,
                    CreatedAt = _faker.Date.Between(discussion.CreatedAt, Now)
                });
            }

            // Each user follows 0-3 spaces
            var spaceCount = _faker.Random.Int(0, 3);
            var pickedSpaces = _faker.PickRandom(spaces, Math.Min(spaceCount, spaces.Count));
            foreach (var space in pickedSpaces)
            {
                follows.Add(new FollowDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = user.Id,
                    TargetTypeId = (int)FollowTargetTypeEnum.Space,
                    LevelId = _faker.PickRandom(FollowLevelEnum.DiscussionsOnly, FollowLevelEnum.DiscussionsAndPosts) == FollowLevelEnum.DiscussionsOnly
                        ? (int)FollowLevelEnum.DiscussionsOnly
                        : (int)FollowLevelEnum.DiscussionsAndPosts,
                    SpaceId = space.Id,
                    CreatedAt = _faker.Date.Between(space.CreatedAt, Now)
                });
            }

            // Each user follows 0-3 other users
            var userFollowCount = _faker.Random.Int(0, 3);
            var otherUsers = users.Where(u => u.Id != user.Id).ToList();
            var pickedUsers = _faker.PickRandom(otherUsers, Math.Min(userFollowCount, otherUsers.Count));
            foreach (var followed in pickedUsers)
            {
                follows.Add(new FollowDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = user.Id,
                    TargetTypeId = (int)FollowTargetTypeEnum.User,
                    LevelId = (int)FollowLevelEnum.DiscussionsAndPosts,
                    FollowedUserId = followed.Id,
                    CreatedAt = _faker.Date.Between(followed.CreatedAt, Now)
                });
            }
        }

        _context.Follows.AddRange(follows);
        await _context.SaveChangesAsync();

        var discussionFollows = follows.Count(f => f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion);
        var spaceFollows = follows.Count(f => f.TargetTypeId == (int)FollowTargetTypeEnum.Space);
        var userFollows = follows.Count(f => f.TargetTypeId == (int)FollowTargetTypeEnum.User);
        Console.WriteLine($"Seeded {follows.Count} follows ({discussionFollows} discussion, {spaceFollows} space, {userFollows} user).");
    }

    private async Task SeedReportsAsync(List<UserDatabaseEntity> users)
    {
        Console.WriteLine("Seeding reports...");

        var reports = new List<ReportDatabaseEntity>();
        var reasons = await _context.ReportReasons.Where(r => !r.IsDeleted).ToListAsync();
        var posts = await _context.Posts
            .Include(p => p.Discussion)
                .ThenInclude(d => d.Space)
                    .ThenInclude(s => s.Hub)
            .Where(p => !p.IsDeleted && !p.IsFirstPost)
            .OrderBy(p => Guid.NewGuid())
            .Take(60)
            .ToListAsync();

        var detailPool = new[]
        {
            "This post is clearly off-topic and doesn't belong here.",
            "User is being hostile towards other members.",
            "This looks like spam or self-promotion.",
            "Contains misleading information.",
            "Repeated low-effort posts from this user.",
            "Potentially harmful advice being given.",
            "User is derailing the discussion on purpose.",
            null, null, null // ~30% no details
        };

        var modCommentPool = new[]
        {
            "Reviewed — appears to violate community guidelines.",
            "User has been warned previously for similar behavior.",
            "Content has been removed. Escalating to community admin.",
            "False positive — the post is fine in context.",
            "Duplicate report — already handled.",
            "Issuing a 3-day temp ban based on pattern of behavior."
        };

        // Create 30-50 reports on random posts
        var reportCount = _faker.Random.Int(30, 50);
        for (var i = 0; i < reportCount && i < posts.Count; i++)
        {
            var post = posts[i];
            var reporter = _faker.PickRandom(users.Where(u => u.Id != post.CreatedByUserId).ToList());
            var reason = reasons.Count > 0 ? _faker.PickRandom(reasons) : null;

            // ~60% pending, ~25% resolved, ~15% dismissed
            var roll = _faker.Random.Double();
            var status = roll < 0.60
                ? ReportStatusEnum.Pending
                : roll < 0.85
                    ? ReportStatusEnum.Resolved
                    : ReportStatusEnum.Dismissed;

            var createdAt = _faker.Date.Between(post.CreatedAt, Now);
            var resolvedByUser = status != ReportStatusEnum.Pending
                ? _faker.PickRandom(users)
                : null;

            var report = new ReportDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                ReporterUserId = reporter.Id,
                ReportedPostId = post.Id,
                ReasonId = reason?.Id,
                Details = _faker.PickRandom(detailPool),
                StatusId = (int)status,
                CreatedAt = createdAt,
                ResolvedAt = status != ReportStatusEnum.Pending
                    ? _faker.Date.Between(createdAt, Now)
                    : null,
                ResolvedByUserId = resolvedByUser?.Id,
                ResolutionNote = status != ReportStatusEnum.Pending
                    ? _faker.PickRandom(modCommentPool)
                    : null,
                SpaceId = post.Discussion.SpaceId,
                HubId = post.Discussion.Space.HubId,
                CommunityId = post.Discussion.Space.Hub.CommunityId
            };

            reports.Add(report);
        }

        _context.Reports.AddRange(reports);
        await _context.SaveChangesAsync();

        // Add mod comments on some resolved/dismissed reports
        var comments = new List<ReportCommentDatabaseEntity>();
        var handledReports = reports.Where(r => r.StatusId != (int)ReportStatusEnum.Pending).ToList();
        foreach (var report in handledReports)
        {
            var commentCount = _faker.Random.Int(0, 2);
            for (var j = 0; j < commentCount; j++)
            {
                var mod = _faker.PickRandom(users);
                comments.Add(new ReportCommentDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    ReportId = report.Id,
                    AuthorUserId = mod.Id,
                    Content = _faker.PickRandom(modCommentPool),
                    CreatedAt = _faker.Date.Between(report.CreatedAt, report.ResolvedAt ?? Now)
                });
            }
        }

        _context.ReportComments.AddRange(comments);
        await _context.SaveChangesAsync();

        var pending = reports.Count(r => r.StatusId == (int)ReportStatusEnum.Pending);
        var resolved = reports.Count(r => r.StatusId == (int)ReportStatusEnum.Resolved);
        var dismissed = reports.Count(r => r.StatusId == (int)ReportStatusEnum.Dismissed);
        Console.WriteLine($"Seeded {reports.Count} reports ({pending} pending, {resolved} resolved, {dismissed} dismissed) with {comments.Count} mod comments.");
    }

    private async Task SeedPostRevisionsAsync(List<UserDatabaseEntity> users)
    {
        Console.WriteLine("Seeding post revisions...");

        // Pick ~10% of non-deleted posts to have edit history
        var posts = await _context.Posts
            .Where(p => !p.IsDeleted && !p.IsFirstPost)
            .OrderBy(p => Guid.NewGuid())
            .Take(80)
            .ToListAsync();

        var editedPosts = _faker.PickRandom(posts, Math.Min(posts.Count, 80)).ToList();
        var revisions = new List<PostRevisionDatabaseEntity>();

        foreach (var post in editedPosts)
        {
            var revisionCount = _faker.Random.Int(1, 3);

            for (var r = 1; r <= revisionCount; r++)
            {
                // Each revision stores the content BEFORE the edit
                var previousContent = r == 1
                    ? post.Content // First revision = original content
                    : GeneratePostContent(isOpeningPost: false);

                revisions.Add(new PostRevisionDatabaseEntity
                {
                    PostId = post.Id,
                    PostPublicId = post.PublicId,
                    Content = previousContent,
                    CreatedAt = _faker.Date.Between(post.CreatedAt, Now),
                    EditedByUserId = post.CreatedByUserId,
                    EditedByUserPublicId = users.First(u => u.Id == post.CreatedByUserId).PublicId,
                    RevisionNumber = r
                });
            }

            // Update the post's RevisionCount and current content
            post.RevisionCount = revisionCount;
            post.Content = GeneratePostContent(isOpeningPost: false);
            post.RenderedContent = _markupParser.ToHtml(post.Content);
        }

        _context.PostRevisions.AddRange(revisions);
        await _context.SaveChangesAsync();

        Console.WriteLine($"Seeded {revisions.Count} revisions across {editedPosts.Count} posts.");
    }

    private async Task SeedModerationDataAsync(List<UserDatabaseEntity> users)
    {
        Console.WriteLine("Seeding moderation data (roles, bans, report reasons)...");

        var adminUser = users.First(u => u.PublicId == "01JJQP0000000000000ADMIN");
        var regularUsers = users.Where(u => u.PublicId != "01JJQP0000000000000ADMIN").ToList();

        var communities = await _context.Communities.ToListAsync();
        var hubs = await _context.Hubs.Include(h => h.Community).ToListAsync();
        var spaces = await _context.Spaces.Include(s => s.Hub).ToListAsync();

        var banReasons = new[]
        {
            "Repeated spam and self-promotion",
            "Harassment of other members",
            "Toxic behavior in discussions",
            "Posting inappropriate content",
            "Trolling and bad-faith arguments",
            "Doxxing or sharing personal information",
            "Evading previous ban",
            "Repeatedly violating community rules",
            "Hate speech",
            "Impersonating another user"
        };

        var reportReasonPool = new (string Name, string Description)[]
        {
            ("Low-effort content", "Posts that don't contribute meaningfully to the discussion"),
            ("Self-promotion", "Excessive promotion of personal projects or products"),
            ("Misinformation", "Spreading false or misleading information"),
            ("Off-topic", "Content that doesn't belong in this space"),
            ("Duplicate post", "Content that has already been posted recently"),
            ("Untagged spoilers", "Spoilers shared without proper spoiler tags"),
            ("Unsolicited advice", "Giving advice when none was asked for"),
            ("Clickbait", "Misleading titles designed to attract clicks"),
            ("AI-generated spam", "Low-quality AI-generated content posted en masse"),
            ("Piracy or illegal content", "Sharing or linking to pirated or illegal material")
        };

        var usedModUserIds = new HashSet<int>();
        var totalRoles = 0;
        var totalBans = 0;
        var totalReportReasons = 0;

        foreach (var community in communities)
        {
            // 1 CommunityAdmin + 2 CommunityMods per community
            var communityMods = _faker.PickRandom(regularUsers, 3).ToList();
            foreach (var mod in communityMods)
                usedModUserIds.Add(mod.Id);

            _context.UserRoles.Add(new UserRoleDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                UserId = communityMods[0].Id,
                RoleId = (int)UserRoleTypeEnum.CommunityAdmin,
                CommunityId = community.Id,
                AssignedByUserId = adminUser.Id,
                AssignedAt = community.CreatedAt.AddHours(_faker.Random.Int(1, 48))
            });

            for (var i = 1; i < communityMods.Count; i++)
            {
                _context.UserRoles.Add(new UserRoleDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = communityMods[i].Id,
                    RoleId = (int)UserRoleTypeEnum.CommunityMod,
                    CommunityId = community.Id,
                    AssignedByUserId = adminUser.Id,
                    AssignedAt = community.CreatedAt.AddDays(_faker.Random.Int(1, 10))
                });
            }

            totalRoles += 3;

            // 0-3 bans per community
            var communityBanCount = _faker.Random.Int(0, 3);
            var bannableUsers = regularUsers.Where(u => !usedModUserIds.Contains(u.Id)).ToList();
            var bannedUsers = _faker.PickRandom(bannableUsers, Math.Min(communityBanCount, bannableUsers.Count)).ToList();

            foreach (var banned in bannedUsers)
            {
                var bannedAt = community.CreatedAt.AddDays(_faker.Random.Int(5, 60));
                var isPermanent = _faker.Random.Bool(0.4f);
                var isExpired = !isPermanent && _faker.Random.Bool(0.3f);

                _context.UserBans.Add(new UserBanDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = banned.Id,
                    BanTypeId = (int)_faker.PickRandom<BanTypeEnum>(),
                    CommunityId = community.Id,
                    Reason = _faker.PickRandom(banReasons),
                    BannedAt = bannedAt,
                    ExpiresAt = isPermanent ? null : bannedAt.AddDays(_faker.Random.Int(7, 90)),
                    BannedByUserId = communityMods[0].Id,
                    UnbannedAt = isExpired ? bannedAt.AddDays(_faker.Random.Int(3, 14)) : null,
                    UnbannedByUserId = isExpired ? communityMods[0].Id : null
                });
            }

            totalBans += bannedUsers.Count;

            // 0-3 custom report reasons per community
            var communityReasonCount = _faker.Random.Int(0, 3);
            var pickedReasons = _faker.PickRandom(reportReasonPool, Math.Min(communityReasonCount, reportReasonPool.Length)).ToList();

            for (var i = 0; i < pickedReasons.Count; i++)
            {
                _context.ReportReasons.Add(new ReportReasonDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    Name = pickedReasons[i].Name,
                    Description = pickedReasons[i].Description,
                    CommunityId = community.Id,
                    CreatedByUserId = communityMods[0].Id,
                    CreatedAt = community.CreatedAt.AddDays(_faker.Random.Int(2, 20)),
                    DisplayOrder = i + 1
                });
            }

            totalReportReasons += pickedReasons.Count;
        }

        foreach (var hub in hubs)
        {
            // 1-3 HubMods per hub
            var hubModCount = _faker.Random.Int(1, 3);
            var availableUsers = regularUsers.Where(u => !usedModUserIds.Contains(u.Id)).ToList();
            var hubMods = _faker.PickRandom(availableUsers, Math.Min(hubModCount, availableUsers.Count)).ToList();

            foreach (var mod in hubMods)
            {
                usedModUserIds.Add(mod.Id);
                _context.UserRoles.Add(new UserRoleDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = mod.Id,
                    RoleId = (int)UserRoleTypeEnum.HubMod,
                    HubId = hub.Id,
                    AssignedByUserId = adminUser.Id,
                    AssignedAt = hub.CreatedAt.AddDays(_faker.Random.Int(1, 14))
                });
            }

            totalRoles += hubMods.Count;

            // 0-3 bans per hub
            var hubBanCount = _faker.Random.Int(0, 3);
            var bannableUsers = regularUsers.Where(u => !usedModUserIds.Contains(u.Id)).ToList();
            var bannedUsers = _faker.PickRandom(bannableUsers, Math.Min(hubBanCount, bannableUsers.Count)).ToList();

            foreach (var banned in bannedUsers)
            {
                var bannedAt = hub.CreatedAt.AddDays(_faker.Random.Int(5, 50));
                var isPermanent = _faker.Random.Bool(0.3f);

                _context.UserBans.Add(new UserBanDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = banned.Id,
                    BanTypeId = (int)_faker.PickRandom<BanTypeEnum>(),
                    HubId = hub.Id,
                    Reason = _faker.PickRandom(banReasons),
                    BannedAt = bannedAt,
                    ExpiresAt = isPermanent ? null : bannedAt.AddDays(_faker.Random.Int(3, 60)),
                    BannedByUserId = hubMods.Count > 0 ? hubMods[0].Id : adminUser.Id
                });
            }

            totalBans += bannedUsers.Count;

            // 0-3 custom report reasons per hub
            var hubReasonCount = _faker.Random.Int(0, 3);
            var pickedReasons = _faker.PickRandom(reportReasonPool, Math.Min(hubReasonCount, reportReasonPool.Length)).ToList();

            for (var i = 0; i < pickedReasons.Count; i++)
            {
                _context.ReportReasons.Add(new ReportReasonDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    Name = pickedReasons[i].Name,
                    Description = pickedReasons[i].Description,
                    HubId = hub.Id,
                    CreatedByUserId = hubMods.Count > 0 ? hubMods[0].Id : adminUser.Id,
                    CreatedAt = hub.CreatedAt.AddDays(_faker.Random.Int(3, 15)),
                    DisplayOrder = i + 1
                });
            }

            totalReportReasons += pickedReasons.Count;
        }

        foreach (var space in spaces)
        {
            // 1-2 SpaceMods per space
            var spaceModCount = _faker.Random.Int(1, 2);
            var availableUsers = regularUsers.Where(u => !usedModUserIds.Contains(u.Id)).ToList();
            var spaceMods = _faker.PickRandom(availableUsers, Math.Min(spaceModCount, availableUsers.Count)).ToList();

            foreach (var mod in spaceMods)
            {
                usedModUserIds.Add(mod.Id);
                _context.UserRoles.Add(new UserRoleDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = mod.Id,
                    RoleId = (int)UserRoleTypeEnum.SpaceMod,
                    SpaceId = space.Id,
                    AssignedByUserId = adminUser.Id,
                    AssignedAt = space.CreatedAt.AddDays(_faker.Random.Int(1, 10))
                });
            }

            totalRoles += spaceMods.Count;

            // 0-3 bans per space
            var spaceBanCount = _faker.Random.Int(0, 3);
            var bannableUsers = regularUsers.Where(u => !usedModUserIds.Contains(u.Id)).ToList();
            var bannedUsers = _faker.PickRandom(bannableUsers, Math.Min(spaceBanCount, bannableUsers.Count)).ToList();

            foreach (var banned in bannedUsers)
            {
                var bannedAt = space.CreatedAt.AddDays(_faker.Random.Int(3, 40));
                var isPermanent = _faker.Random.Bool(0.25f);

                _context.UserBans.Add(new UserBanDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    UserId = banned.Id,
                    BanTypeId = (int)_faker.PickRandom<BanTypeEnum>(),
                    SpaceId = space.Id,
                    Reason = _faker.PickRandom(banReasons),
                    BannedAt = bannedAt,
                    ExpiresAt = isPermanent ? null : bannedAt.AddDays(_faker.Random.Int(1, 30)),
                    BannedByUserId = spaceMods.Count > 0 ? spaceMods[0].Id : adminUser.Id
                });
            }

            totalBans += bannedUsers.Count;

            // 0-3 custom report reasons per space
            var spaceReasonCount = _faker.Random.Int(0, 3);
            var pickedReasons = _faker.PickRandom(reportReasonPool, Math.Min(spaceReasonCount, reportReasonPool.Length)).ToList();

            for (var i = 0; i < pickedReasons.Count; i++)
            {
                _context.ReportReasons.Add(new ReportReasonDatabaseEntity
                {
                    PublicId = Ulid.NewUlid().ToString(),
                    Name = pickedReasons[i].Name,
                    Description = pickedReasons[i].Description,
                    SpaceId = space.Id,
                    CreatedByUserId = spaceMods.Count > 0 ? spaceMods[0].Id : adminUser.Id,
                    CreatedAt = space.CreatedAt.AddDays(_faker.Random.Int(2, 12)),
                    DisplayOrder = i + 1
                });
            }

            totalReportReasons += pickedReasons.Count;
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"Created {totalRoles} moderator roles, {totalBans} bans, {totalReportReasons} custom report reasons.");
    }

    // ===== GROUP ACCESS CONTROL =====

    private async Task SeedGroupsAndAccessAsync(
        CommunityDatabaseEntity snakkCommunity,
        List<UserDatabaseEntity> users)
    {
        // Pick a set of regular users to assign to groups (skip test user and admin)
        var regularUsers = users
            .Where(u => u.PublicId != "01JJQP0000000000000000TEST" && u.PublicId != "01JJQP0000000000000ADMIN")
            .ToList();

        var adminUser = users.First(u => u.PublicId == "01JJQP0000000000000ADMIN");

        // Create 3 groups for the Snakk community
        var premiumGroup = new GroupDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = snakkCommunity.Id,
            Name = "Premium Members",
            Slug = "premium-members",
            Description = "Paid supporters with elevated access.",
            IsPublic = true,
            SortOrder = 0,
            CreatedAt = EarliestDate
        };

        var betaGroup = new GroupDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = snakkCommunity.Id,
            Name = "Beta Testers",
            Slug = "beta-testers",
            Description = "Early access to new features.",
            IsPublic = true,
            SortOrder = 1,
            CreatedAt = EarliestDate
        };

        var staffGroup = new GroupDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = snakkCommunity.Id,
            Name = "Staff",
            Slug = "staff",
            Description = "Internal staff members.",
            IsPublic = false,
            SortOrder = 2,
            CreatedAt = EarliestDate
        };

        _context.Groups.AddRange(premiumGroup, betaGroup, staffGroup);
        await _context.SaveChangesAsync();

        // Assign ~30 users to Premium Members
        var premiumUsers = regularUsers.Take(30).ToList();
        foreach (var user in premiumUsers)
        {
            _context.GroupMembers.Add(new GroupMemberDatabaseEntity
            {
                GroupId = premiumGroup.Id,
                UserId = user.Id,
                AddedByUserId = adminUser.Id,
                AddedAt = EarliestDate.AddDays(_faker.Random.Int(1, 10))
            });
        }

        // Assign ~15 users to Beta Testers (some overlap with premium)
        var betaUsers = regularUsers.Skip(10).Take(15).ToList();
        foreach (var user in betaUsers)
        {
            _context.GroupMembers.Add(new GroupMemberDatabaseEntity
            {
                GroupId = betaGroup.Id,
                UserId = user.Id,
                AddedByUserId = adminUser.Id,
                AddedAt = EarliestDate.AddDays(_faker.Random.Int(1, 10))
            });
        }

        // Assign 5 users to Staff
        var staffUsers = regularUsers.Skip(50).Take(5).ToList();
        foreach (var user in staffUsers)
        {
            _context.GroupMembers.Add(new GroupMemberDatabaseEntity
            {
                GroupId = staffGroup.Id,
                UserId = user.Id,
                AddedByUserId = adminUser.Id,
                AddedAt = EarliestDate.AddDays(_faker.Random.Int(1, 10))
            });
        }

        await _context.SaveChangesAsync();

        // Mark the AI & Machine Learning space as IsRestricted
        // and grant Premium Members read+write access
        var aiMlSpace = await _context.Spaces
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Slug == "ai-ml");

        if (aiMlSpace is not null)
        {
            aiMlSpace.IsRestricted = true;
            await _context.SaveChangesAsync();

            _context.GroupAccess.Add(new GroupAccessDatabaseEntity
            {
                GroupId = premiumGroup.Id,
                SpaceId = aiMlSpace.Id,
                CanRead = true,
                CanWrite = true,
                CreatedAt = EarliestDate
            });

            // Beta testers can read but not write
            _context.GroupAccess.Add(new GroupAccessDatabaseEntity
            {
                GroupId = betaGroup.Id,
                SpaceId = aiMlSpace.Id,
                CanRead = true,
                CanWrite = false,
                CreatedAt = EarliestDate
            });
        }

        // Mark the DevOps & Cloud space as IsRestricted
        // and grant only Staff access
        var devopsSpace = await _context.Spaces
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Slug == "devops");

        if (devopsSpace is not null)
        {
            devopsSpace.IsRestricted = true;
            await _context.SaveChangesAsync();

            _context.GroupAccess.Add(new GroupAccessDatabaseEntity
            {
                GroupId = staffGroup.Id,
                SpaceId = devopsSpace.Id,
                CanRead = true,
                CanWrite = true,
                CreatedAt = EarliestDate
            });
        }

        await _context.SaveChangesAsync();

        Console.WriteLine($"Created 3 groups (Premium Members, Beta Testers, Staff), assigned {premiumUsers.Count + betaUsers.Count + staffUsers.Count} memberships, restricted 2 spaces.");
    }
}
