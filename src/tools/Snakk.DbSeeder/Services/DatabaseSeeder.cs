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
        await EnsureDefaultAdminExistsAsync();
        await GenerateAllAvatarsAsync();
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

        Console.WriteLine("Database seeding completed successfully.");

        // Separate avatar generation phase
        await GenerateAllAvatarsAsync();
    }

    private async Task ClearExistingDataAsync()
    {
        // Delete in correct order due to foreign keys
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

        // Now create posts for each discussion
        foreach (var discussion in discussions)
        {
            var author = users.First(u => u.Id == discussion.CreatedByUserId);

            // First post (opening post) — usually longer
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
                RevisionCount = 0
            });

            // Variable number of replies
            var replyCount = GetSkewedReplyCount();
            var lastActivityAt = discussion.CreatedAt;

            // Time budget: from discussion creation to latest allowed
            var replyTimeWindow = (latestAllowed - discussion.CreatedAt).TotalMinutes;

            for (var j = 0; j < replyCount; j++)
            {
                var replyAuthor = _faker.PickRandom(users);

                // Each reply is some time after the last, but capped to not exceed Now
                var maxDelay = Math.Max(5, replyTimeWindow / (replyCount + 1));
                var delay = _faker.Random.Double(5, Math.Min(maxDelay, 60 * 24 * 3)); // Up to 3 days, capped
                var replyCreatedAt = lastActivityAt.AddMinutes(delay);

                // Hard cap: never exceed 1 hour ago
                if (replyCreatedAt >= latestAllowed)
                    replyCreatedAt = latestAllowed.AddMinutes(-_faker.Random.Int(1, 60));

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
                    RevisionCount = 0
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

        // Add short random suffix for uniqueness
        slug += "-" + Guid.NewGuid().ToString("N")[..6];
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
}
