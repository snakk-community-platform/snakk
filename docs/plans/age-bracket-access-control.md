# Age-Bracket Access Control System - Implementation Plan

## Overview
Privacy-respecting age-based feature restriction system for Snakk forum platform. Collects DOB once at registration, never stores it permanently, uses encrypted audit logs, and automatically transitions users between age brackets.

---

## Core Design Principles

1. **DOB collected once** - Only at registration, immediately destroyed after processing
2. **No re-verification** - Users automatically transition based on stored dates
3. **Privacy-first** - Transition dates encrypted, no user-visible logs
4. **Monthly batching** - Transitions happen on 1st day of month after birthday (privacy obfuscation)
5. **Configurable brackets** - Admin can define brackets without schema changes

---

## Phase 1: Database Schema & Entities

### 1.1 Shared Enums

**File**: `Snakk.Shared/Enums/AchievementCategoryEnum.cs` (reusing existing pattern)

```csharp
public enum AgeBracketTier
{
    Child = 1,      // Under 13
    Teen = 2,       // 13-17
    Adult = 3,      // 18+
    Verified = 4    // ID-verified adults (future)
}
```

### 1.2 Database Entities

**File**: `Snakk.Infrastructure.Database/Entities/AgeBracketDatabaseEntity.cs`

```csharp
public class AgeBracketDatabaseEntity
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int MinimumAge { get; set; }
    public int? MaximumAge { get; set; } // NULL = no upper limit
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**File**: `Snakk.Infrastructure.Database/Entities/UserBracketDatabaseEntity.cs`

```csharp
public class UserBracketDatabaseEntity
{
    public Guid UserId { get; set; } // PK, FK to Users
    public int CurrentBracketId { get; set; }
    public int? NextBracketId { get; set; } // NULL for adult bracket
    public DateTime? NextTransitionDate { get; set; } // NULL for adult bracket
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public UserDatabaseEntity User { get; set; } = null!;
    public AgeBracketDatabaseEntity CurrentBracket { get; set; } = null!;
    public AgeBracketDatabaseEntity? NextBracket { get; set; }
}
```

**File**: `Snakk.Infrastructure.Database/Entities/BracketFeaturePermissionDatabaseEntity.cs`

```csharp
public class BracketFeaturePermissionDatabaseEntity
{
    public int BracketId { get; set; }
    public string FeatureName { get; set; } = null!; // e.g., "CreateDiscussion", "SendDirectMessage"
    public bool Allowed { get; set; }

    // Navigation
    public AgeBracketDatabaseEntity Bracket { get; set; } = null!;
}
```

**File**: `Snakk.Infrastructure.Database/Entities/BracketAuditDatabaseEntity.cs`

```csharp
public class BracketAuditDatabaseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int? FromBracketId { get; set; } // NULL for registration
    public int ToBracketId { get; set; }

    // ENCRYPTED - stores encrypted DateTime string
    public string EncryptedTransitionDate { get; set; } = null!;

    public string? Trigger { get; set; } // "registration", "scheduled_transition", "admin_override"
    public DateTime LoggedAt { get; set; }

    // Navigation
    public UserDatabaseEntity User { get; set; } = null!;
}
```

### 1.3 DbContext Configuration

**File**: `Snakk.Infrastructure.Database/SnakkDbContext.cs`

```csharp
public DbSet<AgeBracketDatabaseEntity> AgeBrackets { get; set; } = null!;
public DbSet<UserBracketDatabaseEntity> UserBrackets { get; set; } = null!;
public DbSet<BracketFeaturePermissionDatabaseEntity> BracketFeaturePermissions { get; set; } = null!;
public DbSet<BracketAuditDatabaseEntity> BracketAudits { get; set; } = null!;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Age Brackets
    modelBuilder.Entity<AgeBracketDatabaseEntity>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.PublicId).IsUnique();
        entity.HasIndex(e => e.Slug).IsUnique();
        entity.HasIndex(e => new { e.IsActive, e.DisplayOrder });

        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Slug).HasMaxLength(50).IsRequired();
    });

    // User Brackets
    modelBuilder.Entity<UserBracketDatabaseEntity>(entity =>
    {
        entity.HasKey(e => e.UserId);

        entity.HasOne(e => e.User)
            .WithOne()
            .HasForeignKey<UserBracketDatabaseEntity>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.CurrentBracket)
            .WithMany()
            .HasForeignKey(e => e.CurrentBracketId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.NextBracket)
            .WithMany()
            .HasForeignKey(e => e.NextBracketId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.NextTransitionDate);
    });

    // Bracket Feature Permissions
    modelBuilder.Entity<BracketFeaturePermissionDatabaseEntity>(entity =>
    {
        entity.HasKey(e => new { e.BracketId, e.FeatureName });

        entity.HasOne(e => e.Bracket)
            .WithMany()
            .HasForeignKey(e => e.BracketId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(e => e.FeatureName).HasMaxLength(100).IsRequired();
    });

    // Bracket Audit (encrypted)
    modelBuilder.Entity<BracketAuditDatabaseEntity>(entity =>
    {
        entity.HasKey(e => e.Id);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.LoggedAt);

        // Encrypted column stored as TEXT
        entity.Property(e => e.EncryptedTransitionDate)
            .HasColumnType("TEXT")
            .IsRequired();
    });
}
```

---

## Phase 2: Encryption Infrastructure

### 2.1 Encryption Service

**File**: `Snakk.Infrastructure/Services/DataEncryptionService.cs`

```csharp
public interface IDataEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public class DataEncryptionService : IDataEncryptionService
{
    private readonly byte[] _key;

    public DataEncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["Encryption:AuditDataKey"]
            ?? throw new InvalidOperationException("Encryption key not configured");
        _key = Convert.FromBase64String(keyString);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();

        // Prepend IV to ciphertext
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var buffer = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[aes.IV.Length];
        Array.Copy(buffer, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }
}
```

### 2.2 EF Core Value Converter

**File**: `Snakk.Infrastructure.Database/Converters/EncryptedDateConverter.cs`

```csharp
public class EncryptedDateConverter : ValueConverter<DateTime, string>
{
    public EncryptedDateConverter(IDataEncryptionService encryptionService)
        : base(
            // Encrypt on write to DB
            v => encryptionService.Encrypt(v.ToString("O")),
            // Decrypt on read from DB
            v => DateTime.Parse(encryptionService.Decrypt(v), null, System.Globalization.DateTimeStyles.RoundtripKind)
        )
    {
    }
}
```

### 2.3 Apply Converter in DbContext

Update `SnakkDbContext.OnModelCreating`:

```csharp
// In BracketAuditDatabaseEntity configuration
entity.Property(e => e.EncryptedTransitionDate)
    .HasConversion(new EncryptedDateConverter(_encryptionService))
    .HasColumnType("TEXT");
```

Update DbContext constructor:

```csharp
private readonly IDataEncryptionService _encryptionService;

public SnakkDbContext(
    DbContextOptions<SnakkDbContext> options,
    IDataEncryptionService encryptionService)
    : base(options)
{
    _encryptionService = encryptionService;
}
```

---

## Phase 3: Domain Layer

### 3.1 Value Objects

**File**: `Snakk.Domain/ValueObjects/AgeBracketId.cs`

```csharp
public record AgeBracketId
{
    public string Value { get; }
    private AgeBracketId(string value) => Value = value;
    public static AgeBracketId From(string value) => new(value);
    public static AgeBracketId New() => new(Ulid.NewUlid().ToString());
    public static implicit operator string(AgeBracketId id) => id.Value;
}
```

### 3.2 Domain Entities

**File**: `Snakk.Domain/Entities/AgeBracket.cs`

```csharp
public class AgeBracket
{
    public AgeBracketId PublicId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public int MinimumAge { get; private set; }
    public int? MaximumAge { get; private set; }
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    private AgeBracket() { }

    public static AgeBracket Create(
        string name,
        string slug,
        int minimumAge,
        int? maximumAge,
        string? description,
        int displayOrder)
    {
        return new AgeBracket
        {
            PublicId = AgeBracketId.New(),
            Name = name,
            Slug = slug,
            MinimumAge = minimumAge,
            MaximumAge = maximumAge,
            Description = description,
            DisplayOrder = displayOrder,
            IsActive = true
        };
    }

    public bool AppliesTo(int age) =>
        age >= MinimumAge && (MaximumAge == null || age < MaximumAge);
}
```

**File**: `Snakk.Domain/Entities/UserBracket.cs`

```csharp
public class UserBracket
{
    public UserId UserId { get; private set; }
    public AgeBracketId CurrentBracketId { get; private set; }
    public AgeBracketId? NextBracketId { get; private set; }
    public DateTime? NextTransitionDate { get; private set; }

    private UserBracket() { }

    public static UserBracket Create(
        UserId userId,
        AgeBracketId currentBracketId,
        AgeBracketId? nextBracketId,
        DateTime? nextTransitionDate)
    {
        return new UserBracket
        {
            UserId = userId,
            CurrentBracketId = currentBracketId,
            NextBracketId = nextBracketId,
            NextTransitionDate = nextTransitionDate
        };
    }

    public bool RequiresTransition(DateTime currentDate) =>
        NextTransitionDate.HasValue && NextTransitionDate.Value <= currentDate;
}
```

### 3.3 Repository Interfaces

**File**: `Snakk.Domain/Repositories/IAgeBracketRepository.cs`

```csharp
public interface IAgeBracketRepository
{
    Task<AgeBracket?> GetByIdAsync(AgeBracketId id);
    Task<AgeBracket?> GetBySlugAsync(string slug);
    Task<IEnumerable<AgeBracket>> GetAllActiveAsync();
    Task<AgeBracket?> GetBracketForAgeAsync(int age);
    Task<AgeBracket?> GetNextBracketAsync(int currentAge);
    Task AddAsync(AgeBracket bracket);
    Task UpdateAsync(AgeBracket bracket);
}

public interface IUserBracketRepository
{
    Task<UserBracket?> GetByUserIdAsync(UserId userId);
    Task<IEnumerable<UserBracket>> GetUsersRequiringTransitionAsync(DateTime date);
    Task AddAsync(UserBracket userBracket);
    Task UpdateAsync(UserBracket userBracket);
}

public interface IBracketAuditRepository
{
    Task LogTransitionAsync(
        UserId userId,
        AgeBracketId? fromBracketId,
        AgeBracketId toBracketId,
        DateTime transitionDate,
        string trigger);
    Task<IEnumerable<BracketAudit>> GetUserHistoryAsync(UserId userId, string adminReason);
}
```

---

## Phase 4: Application Services

### 4.1 Age Bracket Service

**File**: `Snakk.Application/Services/AgeBracketService.cs`

```csharp
public class AgeBracketService
{
    private readonly IAgeBracketRepository _bracketRepo;
    private readonly IUserBracketRepository _userBracketRepo;
    private readonly IBracketAuditRepository _auditRepo;

    public AgeBracketService(
        IAgeBracketRepository bracketRepo,
        IUserBracketRepository userBracketRepo,
        IBracketAuditRepository auditRepo)
    {
        _bracketRepo = bracketRepo;
        _userBracketRepo = userBracketRepo;
        _auditRepo = auditRepo;
    }

    /// <summary>
    /// Assigns initial bracket to new user. DOB is NEVER stored.
    /// </summary>
    public async Task AssignInitialBracketAsync(UserId userId, DateTime dateOfBirth)
    {
        var age = CalculateAge(dateOfBirth);
        var currentBracket = await _bracketRepo.GetBracketForAgeAsync(age)
            ?? throw new InvalidOperationException("No bracket found for user's age");

        var (nextBracket, transitionDate) = await CalculateNextTransitionAsync(dateOfBirth, currentBracket);

        var userBracket = UserBracket.Create(
            userId,
            currentBracket.PublicId,
            nextBracket?.PublicId,
            transitionDate);

        await _userBracketRepo.AddAsync(userBracket);
        await _auditRepo.LogTransitionAsync(
            userId,
            fromBracketId: null,
            currentBracket.PublicId,
            DateTime.UtcNow,
            "registration");
    }

    /// <summary>
    /// Process scheduled transitions (called by background job on 1st of month).
    /// </summary>
    public async Task ProcessScheduledTransitionsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var usersToTransition = await _userBracketRepo.GetUsersRequiringTransitionAsync(today);

        foreach (var userBracket in usersToTransition)
        {
            if (!userBracket.NextBracketId.HasValue)
                continue;

            var fromBracketId = userBracket.CurrentBracketId;
            var toBracketId = userBracket.NextBracketId.Value;

            // Calculate next transition after this one
            var nextBracket = await _bracketRepo.GetByIdAsync(toBracketId);
            if (nextBracket == null) continue;

            var (futureNextBracket, futureTransitionDate) =
                await CalculateNextTransitionFromBracketAsync(userBracket.NextTransitionDate!.Value, nextBracket);

            // Update user bracket
            var updatedBracket = UserBracket.Create(
                userBracket.UserId,
                toBracketId,
                futureNextBracket?.PublicId,
                futureTransitionDate);

            await _userBracketRepo.UpdateAsync(updatedBracket);
            await _auditRepo.LogTransitionAsync(
                userBracket.UserId,
                fromBracketId,
                toBracketId,
                DateTime.UtcNow,
                "scheduled_transition");
        }
    }

    /// <summary>
    /// Check if user has permission for a feature.
    /// </summary>
    public async Task<bool> HasFeaturePermissionAsync(UserId userId, string featureName)
    {
        var userBracket = await _userBracketRepo.GetByUserIdAsync(userId);
        if (userBracket == null)
            return false;

        var bracket = await _bracketRepo.GetByIdAsync(userBracket.CurrentBracketId);
        // Feature permission logic would be implemented here
        return true; // Placeholder
    }

    private int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }

    private async Task<(AgeBracket? nextBracket, DateTime? transitionDate)>
        CalculateNextTransitionAsync(DateTime dateOfBirth, AgeBracket currentBracket)
    {
        if (currentBracket.MaximumAge == null)
            return (null, null); // Adult bracket, no transitions

        var transitionAge = currentBracket.MaximumAge.Value;
        var birthdayAtTransition = dateOfBirth.AddYears(transitionAge);

        // Transition on 1st of month AFTER birthday month (privacy obfuscation)
        var transitionDate = new DateTime(
            birthdayAtTransition.Year,
            birthdayAtTransition.Month,
            1).AddMonths(1);

        var nextBracket = await _bracketRepo.GetBracketForAgeAsync(transitionAge);
        return (nextBracket, transitionDate);
    }

    private async Task<(AgeBracket? nextBracket, DateTime? transitionDate)>
        CalculateNextTransitionFromBracketAsync(DateTime lastTransitionDate, AgeBracket currentBracket)
    {
        if (currentBracket.MaximumAge == null)
            return (null, null);

        // Calculate years until next transition
        var yearsUntilTransition = currentBracket.MaximumAge.Value - currentBracket.MinimumAge;
        var nextTransitionDate = lastTransitionDate.AddYears(yearsUntilTransition);

        var nextBracket = await _bracketRepo.GetBracketForAgeAsync(currentBracket.MaximumAge.Value);
        return (nextBracket, nextTransitionDate);
    }
}
```

---

## Phase 5: Background Jobs

### 5.1 Monthly Transition Job

**File**: `Snakk.Infrastructure/BackgroundJobs/AgeBracketTransitionJob.cs`

```csharp
public class AgeBracketTransitionJob : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgeBracketTransitionJob> _logger;
    private Timer? _timer;

    public AgeBracketTransitionJob(
        IServiceProvider serviceProvider,
        ILogger<AgeBracketTransitionJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Age Bracket Transition Job started");

        // Run daily at 2 AM UTC, check if it's the 1st of month
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddDays(1).AddHours(2);
        var delay = nextRun - now;

        _timer = new Timer(
            ProcessTransitions,
            null,
            delay,
            TimeSpan.FromDays(1));

        return Task.CompletedTask;
    }

    private async void ProcessTransitions(object? state)
    {
        if (DateTime.UtcNow.Day != 1)
            return; // Only run on 1st of month

        _logger.LogInformation("Running monthly age bracket transitions");

        using var scope = _serviceProvider.CreateScope();
        var bracketService = scope.ServiceProvider.GetRequiredService<AgeBracketService>();

        try
        {
            await bracketService.ProcessScheduledTransitionsAsync();
            _logger.LogInformation("Age bracket transitions completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing age bracket transitions");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

---

## Phase 6: Authorization & Middleware

### 6.1 Feature Authorization Attribute

**File**: `Snakk.Api/Authorization/RequireFeatureAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireFeatureAttribute : Attribute, IAuthorizationRequirement
{
    public string FeatureName { get; }

    public RequireFeatureAttribute(string featureName)
    {
        FeatureName = featureName;
    }
}

public class FeatureAuthorizationHandler : AuthorizationHandler<RequireFeatureAttribute>
{
    private readonly AgeBracketService _bracketService;

    public FeatureAuthorizationHandler(AgeBracketService bracketService)
    {
        _bracketService = bracketService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireFeatureAttribute requirement)
    {
        var userIdClaim = context.User.FindFirst("user_id");
        if (userIdClaim == null)
        {
            context.Fail();
            return;
        }

        var userId = UserId.From(userIdClaim.Value);
        var hasPermission = await _bracketService.HasFeaturePermissionAsync(
            userId,
            requirement.FeatureName);

        if (hasPermission)
            context.Succeed(requirement);
        else
            context.Fail();
    }
}
```

### 6.2 Controller Usage Example

```csharp
[ApiController]
[Route("api/discussions")]
public class DiscussionsController : ControllerBase
{
    [HttpPost]
    [RequireFeature("CreateDiscussion")]
    public async Task<IActionResult> CreateDiscussion([FromBody] CreateDiscussionRequest request)
    {
        // Only users in brackets with CreateDiscussion=true can access
        // ...
    }
}
```

---

## Phase 7: Registration Flow Integration

### 7.1 Update Registration Use Case

**File**: `Snakk.Application/UseCases/RegisterUserUseCase.cs`

```csharp
public class RegisterUserUseCase
{
    private readonly IUserRepository _userRepo;
    private readonly AgeBracketService _bracketService;
    private readonly IPasswordHasher _passwordHasher;

    public async Task<Result<User>> ExecuteAsync(
        string email,
        string username,
        string password,
        DateTime dateOfBirth) // NEW PARAMETER
    {
        // Validate age (e.g., minimum 13 per COPPA)
        var age = CalculateAge(dateOfBirth);
        if (age < 13)
            return Result<User>.Failure("Users must be at least 13 years old");

        // Create user entity
        var user = User.Create(email, username, _passwordHasher.Hash(password));
        await _userRepo.AddAsync(user);

        // Assign age bracket (DOB is destroyed after this call)
        await _bracketService.AssignInitialBracketAsync(user.Id, dateOfBirth);

        return Result<User>.Success(user);
    }

    private int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }
}
```

### 7.2 Update Registration Page

**File**: `Snakk.Web/Pages/Auth/Register.cshtml`

```html
<form method="post">
    <div class="form-group">
        <label for="Email">Email</label>
        <input type="email" id="Email" name="Email" required />
    </div>

    <div class="form-group">
        <label for="Username">Username</label>
        <input type="text" id="Username" name="Username" required />
    </div>

    <div class="form-group">
        <label for="Password">Password</label>
        <input type="password" id="Password" name="Password" required />
    </div>

    <!-- NEW DATE OF BIRTH FIELD -->
    <div class="form-group">
        <label for="DateOfBirth">Date of Birth</label>
        <input type="date" id="DateOfBirth" name="DateOfBirth" required
               max="@DateTime.UtcNow.AddYears(-13).ToString("yyyy-MM-dd")" />
        <small class="form-text text-muted">
            Your date of birth is used only to determine age-appropriate features.
            It is never stored permanently.
        </small>
    </div>

    <button type="submit">Register</button>
</form>
```

---

## Phase 8: Database Migration & Seeding

### 8.1 Migration

```bash
dotnet ef migrations add AddAgeBracketSystem --project src/core/Snakk.Infrastructure.Database
```

### 8.2 Seed Default Brackets

**File**: `Snakk.Infrastructure.Database/Seeders/AgeBracketSeeder.cs`

```csharp
public static class AgeBracketSeeder
{
    public static void SeedAgeBrackets(SnakkDbContext context)
    {
        if (context.AgeBrackets.Any())
            return;

        var brackets = new[]
        {
            new AgeBracketDatabaseEntity
            {
                PublicId = Guid.NewGuid(),
                Name = "Teen",
                Slug = "teen",
                MinimumAge = 13,
                MaximumAge = 18,
                Description = "Users aged 13-17",
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgeBracketDatabaseEntity
            {
                PublicId = Guid.NewGuid(),
                Name = "Adult",
                Slug = "adult",
                MinimumAge = 18,
                MaximumAge = null,
                Description = "Users 18 and older",
                DisplayOrder = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.AgeBrackets.AddRange(brackets);
        context.SaveChanges();

        // Seed default permissions
        var teenBracket = brackets[0];
        var adultBracket = brackets[1];

        var permissions = new[]
        {
            // Teen restrictions
            new BracketFeaturePermissionDatabaseEntity
            {
                BracketId = teenBracket.Id,
                FeatureName = "CreateDiscussion",
                Allowed = true
            },
            new BracketFeaturePermissionDatabaseEntity
            {
                BracketId = teenBracket.Id,
                FeatureName = "SendDirectMessage",
                Allowed = false // Restrict DMs for teens
            },
            new BracketFeaturePermissionDatabaseEntity
            {
                BracketId = teenBracket.Id,
                FeatureName = "UploadMedia",
                Allowed = false
            },

            // Adult permissions (all enabled)
            new BracketFeaturePermissionDatabaseEntity
            {
                BracketId = adultBracket.Id,
                FeatureName = "CreateDiscussion",
                Allowed = true
            },
            new BracketFeaturePermissionDatabaseEntity
            {
                BracketId = adultBracket.Id,
                FeatureName = "SendDirectMessage",
                Allowed = true
            },
            new BracketFeaturePermissionDatabaseEntity
            {
                BracketId = adultBracket.Id,
                FeatureName = "UploadMedia",
                Allowed = true
            }
        };

        context.BracketFeaturePermissions.AddRange(permissions);
        context.SaveChanges();
    }
}
```

---

## Phase 9: Configuration & Deployment

### 9.1 Encryption Key Setup

**File**: `appsettings.json`

```json
{
  "Encryption": {
    "AuditDataKey": "GENERATE_WITH: openssl rand -base64 32"
  }
}
```

**Production Setup**:
```bash
# Generate encryption key
openssl rand -base64 32

# Store in environment variable (do NOT commit to repo)
export ENCRYPTION__AUDITDATAKEY="generated_key_here"
```

### 9.2 Service Registration

**File**: `Snakk.Api/ServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddAgeBracketServices(
    this IServiceCollection services)
{
    // Encryption
    services.AddSingleton<IDataEncryptionService, DataEncryptionService>();

    // Repositories
    services.AddScoped<IAgeBracketRepository, AgeBracketRepositoryAdapter>();
    services.AddScoped<IUserBracketRepository, UserBracketRepositoryAdapter>();
    services.AddScoped<IBracketAuditRepository, BracketAuditRepositoryAdapter>();

    // Services
    services.AddScoped<AgeBracketService>();

    // Background jobs
    services.AddHostedService<AgeBracketTransitionJob>();

    // Authorization
    services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();

    return services;
}
```

---

## Phase 10: Testing Strategy

### 10.1 Unit Tests

```csharp
public class AgeBracketServiceTests
{
    [Fact]
    public async Task AssignInitialBracket_UserAge15_AssignsTeenBracket()
    {
        // Arrange
        var dob = DateTime.UtcNow.AddYears(-15);
        var userId = UserId.New();

        // Act
        await _service.AssignInitialBracketAsync(userId, dob);

        // Assert
        var userBracket = await _userBracketRepo.GetByUserIdAsync(userId);
        Assert.NotNull(userBracket);
        Assert.Equal("teen", userBracket.CurrentBracket.Slug);
    }

    [Fact]
    public async Task CalculateTransitionDate_TeenUser_ReturnsFirstOfMonthAfterBirthday()
    {
        // Test that transition is obfuscated to 1st of month
        var dob = new DateTime(2010, 6, 15); // Birthday on June 15
        var expectedTransition = new DateTime(2028, 7, 1); // Transition on July 1

        // Assert transition date calculation
    }

    [Fact]
    public async Task ProcessScheduledTransitions_OnFirstOfMonth_TransitionsEligibleUsers()
    {
        // Test batch transition logic
    }
}
```

### 10.2 Integration Tests

```csharp
public class AgeBracketIntegrationTests
{
    [Fact]
    public async Task Registration_WithDOB_CreatesBracketRecordWithoutStoringDOB()
    {
        // Verify DOB is never persisted to database
    }

    [Fact]
    public async Task Authorization_TeenUser_CannotAccessRestrictedFeatures()
    {
        // Test [RequireFeature] attribute enforcement
    }

    [Fact]
    public async Task Encryption_AuditLog_StoresEncryptedDates()
    {
        // Verify dates in BracketAudit are encrypted at rest
    }
}
```

---

## Deployment Checklist

- [ ] Generate encryption key with `openssl rand -base64 32`
- [ ] Store encryption key in environment variable (NOT in repo)
- [ ] Run database migration: `dotnet ef database update`
- [ ] Seed age brackets: Execute `AgeBracketSeeder.SeedAgeBrackets()`
- [ ] Verify background job is registered in DI container
- [ ] Test registration flow with DOB capture
- [ ] Verify DOB is not stored in `Users` table
- [ ] Test transition date calculation (unit tests)
- [ ] Verify audit logs are encrypted (check raw DB)
- [ ] Test authorization attributes on protected endpoints
- [ ] Configure monitoring for transition job failures
- [ ] Document COPPA compliance measures

---

## Privacy Compliance Notes

### COPPA Compliance (Children's Online Privacy Protection Act)
- ✅ Collects DOB only at registration
- ✅ DOB immediately destroyed after bracket assignment
- ✅ No parental consent required (DOB not retained)
- ✅ Age verification happens once, no re-verification

### GDPR Compliance
- ✅ Transition dates encrypted (pseudonymization)
- ✅ No birth dates stored (data minimization)
- ✅ Audit logs can be deleted on user request (right to erasure)
- ✅ No profiling based on exact age

### User Transparency
- Clear messaging on registration: "Your date of birth is used only to determine age-appropriate features and is never stored permanently"
- Privacy policy should explain bracket system
- Users can view their current bracket (but not transition history without admin access)

---

## Future Enhancements

1. **Admin Dashboard**
   - View bracket distribution stats
   - Manual bracket overrides (with audit logging)
   - Configure feature permissions via UI

2. **Verified Adult Tier**
   - Optional ID verification for age-restricted content
   - Integration with identity verification services
   - Separate "verified" badge/tier

3. **Parental Controls**
   - Parent can create "supervised" teen accounts
   - Parent dashboard to view teen activity
   - Separate consent flow for under-13 (if allowed by jurisdiction)

4. **Grace Periods**
   - Allow 7-day grace period after transition
   - Notify user before feature restrictions apply
   - Smooth UX for birthday transitions

---

## Implementation Timeline

**Week 1**: Phase 1-2 (Database schema, encryption)
**Week 2**: Phase 3-4 (Domain layer, application services)
**Week 3**: Phase 5-6 (Background jobs, authorization)
**Week 4**: Phase 7-8 (Registration integration, seeding)
**Week 5**: Phase 9-10 (Testing, deployment)

**Total estimated effort**: 5 weeks

---

## Security Considerations

1. **Encryption Key Management**
   - Use environment variables, never commit to repo
   - Rotate keys annually (requires re-encryption utility)
   - Use separate keys for dev/staging/production

2. **Audit Log Access**
   - Only admins can decrypt audit logs
   - Log all audit log access (meta-audit)
   - Consider write-only audit logs (no decryption in normal ops)

3. **Transition Date Obfuscation**
   - Monthly batching prevents exact birthday inference
   - Even admins cannot determine exact DOB from transition dates
   - Adds up to 30 days of uncertainty

4. **Attack Vectors**
   - Timing attacks: Monthly batching mitigates
   - Social engineering: No way to retrieve DOB (it's destroyed)
   - Database breach: Transition dates are encrypted
   - Admin abuse: All audit log access is logged

---

**END OF IMPLEMENTATION PLAN**