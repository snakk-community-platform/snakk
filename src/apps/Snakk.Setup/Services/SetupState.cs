namespace Snakk.Setup.Services;

/// <summary>
/// Holds wizard state across setup steps. Stored in session via JSON serialization.
/// </summary>
public class SetupState
{
    // Step 2: Database
    public string DbHost { get; set; } = "postgres";
    public int DbPort { get; set; } = 5432;
    public string DbName { get; set; } = "snakk";
    public string DbUsername { get; set; } = "snakk";
    public string DbPassword { get; set; } = "";

    // Step 3: Site Config
    public string Domain { get; set; } = "";
    public string SiteName { get; set; } = "Snakk";
    public string DefaultCommunitySlug { get; set; } = "main";
    public bool MultiCommunityEnabled { get; set; }
    public string Timezone { get; set; } = "UTC";

    // Step 4: Storage
    public string AvatarStoragePath { get; set; } = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snakk", "storage")
        : "/app/storage";
    public string StorageProvider { get; set; } = "Local"; // "Local" or "S3"
    public string S3Endpoint { get; set; } = "";
    public string S3AccessKey { get; set; } = "";
    public string S3SecretKey { get; set; } = "";
    public string S3BucketName { get; set; } = "";
    public string S3PublicUrlBase { get; set; } = "";

    // Step 5: Admin Account
    public string AdminEmail { get; set; } = "";
    public string AdminDisplayName { get; set; } = "";
    public string AdminPassword { get; set; } = "";

    // Step 6: Email/SMTP
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string SmtpSenderEmail { get; set; } = "";
    public string SmtpSenderName { get; set; } = "Snakk";
    public bool SmtpEnabled { get; set; }

    // Step 10: Test Data
    public bool SeedTestData { get; set; }

    // Step 7: Security
    public string JwtSecretKey { get; set; } = "";
    public string RealtimeApiKey { get; set; } = "";
    public string RealtimeJwtKey { get; set; } = "";

    // Turnstile (captcha)
    public string TurnstileSiteKey { get; set; } = "";
    public string TurnstileSecretKey { get; set; } = "";

    // Step 8: OAuth
    public string GoogleClientId { get; set; } = "";
    public string GoogleClientSecret { get; set; } = "";
    public string GitHubClientId { get; set; } = "";
    public string GitHubClientSecret { get; set; } = "";
    public string DiscordClientId { get; set; } = "";
    public string DiscordClientSecret { get; set; } = "";

    // Step 9: First Community
    public string CommunityName { get; set; } = "";
    public string CommunityDescription { get; set; } = "";
    public string FirstHubName { get; set; } = "";
    public string FirstHubSlug { get; set; } = "";
    public string FirstSpaceName { get; set; } = "";
    public string FirstSpaceSlug { get; set; } = "";
    public bool CreateFirstCommunity { get; set; } = true;

    public string GetConnectionString() =>
        $"Host={DbHost};Port={DbPort};Database={DbName};Username={DbUsername};Password={DbPassword}";
}
