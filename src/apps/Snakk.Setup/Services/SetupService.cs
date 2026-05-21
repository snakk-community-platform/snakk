using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Snakk.Setup.Services;

/// <summary>
/// Handles setup wizard operations: DB testing, config writing, DbSeeder invocation.
/// </summary>
public class SetupService()
{
    /// <summary>
    /// Test a PostgreSQL connection by opening and closing it.
    /// Returns null on success, error message on failure.
    /// </summary>
    public async Task<string?> TestDatabaseConnectionAsync(string connectionString)
    {
        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            return null; // success
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Generate a cryptographically secure random string for secrets.
    /// </summary>
    public static string GenerateSecretKey(int length = 64)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes)[..length];
    }

    /// <summary>
    /// Write the snakk-config.json file with all setup values.
    /// </summary>
    public void WriteProductionConfig(SetupState state)
    {
        var config = new Dictionary<string, object>
        {
            ["ConnectionStrings"] = new Dictionary<string, string>
            {
                ["DbConnection"] = state.GetConnectionString(),
                ["AuthDbConnection"] = state.GetConnectionString()
            },
            ["Jwt"] = new Dictionary<string, object>
            {
                ["SecretKey"] = state.JwtSecretKey,
                ["Issuer"] = "Snakk",
                ["Audience"] = "Snakk"
            },
            ["Realtime"] = new Dictionary<string, string>
            {
                ["ApiKey"] = state.RealtimeApiKey,
                ["JwtKey"] = state.RealtimeJwtKey
            },
            ["Snakk"] = new Dictionary<string, object>
            {
                ["Domain"] = state.Domain,
                ["SiteName"] = state.SiteName,
                ["DefaultCommunitySlug"] = state.DefaultCommunitySlug,
                ["PrimaryDomains"] = new[] { state.Domain },
                ["SiteTimezone"] = state.Timezone
            },
            ["Ui"] = new Dictionary<string, object>
            {
                ["Language"] = state.Language
            },
            ["Features"] = new Dictionary<string, object>
            {
                ["MultiCommunityEnabled"] = state.MultiCommunityEnabled,
                ["PasskeysEnabled"] = state.PasskeysEnabled,
                ["TwoFactorEnabled"] = state.TwoFactorEnabled
            },
            ["FileStorage"] = BuildFileStorageConfig(state),
            ["Setup"] = new Dictionary<string, string>
            {
                ["AdminEmail"] = state.AdminEmail,
                ["AdminPassword"] = state.AdminPassword,
                ["AdminDisplayName"] = state.AdminDisplayName
            },
            ["Cors"] = new Dictionary<string, string>
            {
                ["AllowedOrigins"] = $"https://{state.Domain}"
            },
            ["Passkey"] = new Dictionary<string, object>
            {
                ["RelyingPartyId"] = state.Domain,
                ["RelyingPartyName"] = state.SiteName,
                ["Origins"] = IsLocalDomain(state.Domain)
                    ? new[] { "http://localhost", "https://localhost", "http://localhost:17000", "https://localhost:17000" }
                    : new[] { $"https://{state.Domain}" }
            }
        };

        // Add SMTP if enabled
        if (state.SmtpEnabled && !string.IsNullOrWhiteSpace(state.SmtpHost))
        {
            config["Smtp"] = new Dictionary<string, object>
            {
                ["Host"] = state.SmtpHost,
                ["Port"] = state.SmtpPort,
                ["Username"] = state.SmtpUsername,
                ["Password"] = state.SmtpPassword,
                ["SenderEmail"] = state.SmtpSenderEmail,
                ["SenderName"] = state.SmtpSenderName
            };
        }

        // Add Turnstile if configured
        if (!string.IsNullOrWhiteSpace(state.TurnstileSiteKey))
        {
            config["Turnstile"] = new Dictionary<string, string>
            {
                ["SiteKey"] = state.TurnstileSiteKey,
                ["SecretKey"] = state.TurnstileSecretKey
            };
        }

        // Add OAuth if provided
        var auth = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(state.GoogleClientId))
        {
            auth["Google"] = new Dictionary<string, string>
            {
                ["ClientId"] = state.GoogleClientId,
                ["ClientSecret"] = state.GoogleClientSecret
            };
        }
        if (!string.IsNullOrWhiteSpace(state.GitHubClientId))
        {
            auth["GitHub"] = new Dictionary<string, string>
            {
                ["ClientId"] = state.GitHubClientId,
                ["ClientSecret"] = state.GitHubClientSecret
            };
        }
        if (!string.IsNullOrWhiteSpace(state.DiscordClientId))
        {
            auth["Discord"] = new Dictionary<string, string>
            {
                ["ClientId"] = state.DiscordClientId,
                ["ClientSecret"] = state.DiscordClientSecret
            };
        }
        if (auth.Count > 0)
            config["Authentication"] = auth;

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var configPath = Path.Combine(state.AvatarStoragePath, "conf", "snakk-config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, json);

        // Restrict file permissions on Linux/macOS (owner read/write only)
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// Run the DbSeeder tool as a subprocess.
    /// Returns (success, output).
    /// </summary>
    public async Task<(bool Success, string Output)> RunDbSeederAsync(SetupState state)
    {
        // Find DbSeeder — in Docker it's at /app/dbseeder/, locally it's relative
        var seederPath = FindDbSeederPath();
        if (seederPath is null)
        {
            return (false, "Could not find Snakk.DbSeeder.dll. Ensure it's published.");
        }

        var skipSeedFlag = state.SeedTestData ? "" : "--skip-seed";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{seederPath}\" {skipSeedFlag}".Trim(),
            WorkingDirectory = Path.GetDirectoryName(seederPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["ConnectionStrings__DbConnection"] = state.GetConnectionString(),
                ["FileStorage__BasePath"] = state.AvatarStoragePath,
                ["FileStorage__Provider"] = state.StorageProvider == "S3" ? "S3" : "",
                ["FileStorage__S3__Endpoint"] = state.S3Endpoint,
                ["FileStorage__S3__AccessKey"] = state.S3AccessKey,
                ["FileStorage__S3__SecretKey"] = state.S3SecretKey,
                ["FileStorage__S3__BucketName"] = state.S3BucketName,
                ["FileStorage__S3__PublicUrlBase"] = state.S3PublicUrlBase,
                ["Setup__AdminEmail"] = state.AdminEmail,
                ["Setup__AdminPassword"] = state.AdminPassword,
                ["Setup__AdminDisplayName"] = state.AdminDisplayName,
                ["Setup__CommunityName"] = state.CommunityName,
                ["Setup__CommunityDescription"] = state.CommunityDescription,
                ["Setup__FirstHubName"] = state.FirstHubName,
                ["Setup__FirstHubSlug"] = state.FirstHubSlug,
                ["Setup__FirstSpaceName"] = state.FirstSpaceName,
                ["Setup__FirstSpaceSlug"] = state.FirstSpaceSlug,
                ["Setup__CreateFirstCommunity"] = state.CreateFirstCommunity.ToString(),
                ["Snakk__SiteTimezone"] = state.Timezone,
                ["Setup__AllowedDisplayNameScripts"] = string.Join(",", state.AllowedDisplayNameScripts)
            }
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return (false, "Failed to start DbSeeder process.");

            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var combined = output + (string.IsNullOrEmpty(errors) ? "" : "\n" + errors);
            return (process.ExitCode == 0, combined);
        }
        catch (Exception ex)
        {
            return (false, $"Error running DbSeeder: {ex.Message}");
        }
    }

    /// <summary>
    /// Scrub sensitive data from the production config after setup completes.
    /// </summary>
    public void ScrubSensitiveConfig(string storagePath)
    {
        RemoveAdminPasswordFromConfig(storagePath);
    }

    /// <summary>
    /// Remove the Setup:AdminPassword field from snakk-config.json.
    /// The password is only needed during initial DB seeding — once the admin account
    /// exists (with a bcrypt hash), keeping the plaintext is a security risk.
    /// </summary>
    private static void RemoveAdminPasswordFromConfig(string storagePath)
    {
        try
        {
            var configPath = Path.Combine(storagePath, "conf", "snakk-config.json");
            if (!File.Exists(configPath)) return;

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Rebuild config without Setup.AdminPassword
            using var ms = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name == "Setup" && prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        writer.WriteStartObject("Setup");
                        foreach (var setupProp in prop.Value.EnumerateObject())
                        {
                            if (setupProp.Name != "AdminPassword")
                                setupProp.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    else
                        prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            File.WriteAllText(configPath, Encoding.UTF8.GetString(ms.ToArray()));
        }
        catch
        {
            // Non-fatal — don't break setup completion if config scrubbing fails
        }
    }

    /// <summary>
    /// Generate a JWT for auto-login as the admin user after setup.
    /// Queries the database for the admin's PublicId, then creates a token
    /// matching the format used by TokenService.
    /// </summary>
    public async Task<string> GenerateAdminJwtAsync(SetupState state)
    {
        // The admin always has a fixed PublicId assigned by the seeder
        const string publicId = "01JJQP000000000000000ADM1N";

        // Verify the admin was actually created
        await using var conn = new Npgsql.NpgsqlConnection(state.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM \"User\" WHERE \"PublicId\" = @publicId LIMIT 1";
        cmd.Parameters.AddWithValue("publicId", publicId);
        var result = await cmd.ExecuteScalarAsync();
        if (result is null)
            throw new InvalidOperationException("Admin user was not created in the database.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, publicId),
            new(ClaimTypes.Name, state.AdminDisplayName),
            new(ClaimTypes.Email, state.AdminEmail),
            new(ClaimTypes.Role, "GlobalAdmin"),
            new("2fa_enabled", "False")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(state.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "Snakk",
            audience: "Snakk",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string? FindDbSeederPath()
    {
        // Docker path
        var dockerPath = "/app/dbseeder/Snakk.DbSeeder.dll";
        if (File.Exists(dockerPath)) return dockerPath;

        // Local development — search relative to working directory
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "Snakk.DbSeeder", "bin", "Debug", "net10.0", "Snakk.DbSeeder.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "Snakk.DbSeeder", "bin", "Release", "net10.0", "Snakk.DbSeeder.dll"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Start installation in a background thread. Returns immediately.
    /// Progress is tracked via the static <see cref="InstallProgress"/> class.
    /// </summary>
    public void StartInstallInBackground(SetupState state)
    {
        if (InstallProgress.IsRunning)
            return;

        InstallProgress.Reset();
        InstallProgress.IsRunning = true;
        InstallProgress.SeedEnabled = state.SeedTestData;
        InstallProgress.Step = "config";
        InstallProgress.Message = "Writing production configuration...";

        _ = Task.Run(async () => await RunInstallAsync(state));
    }

    private async Task RunInstallAsync(SetupState state)
    {
        try
        {
            // Step 1: Write config
            WriteProductionConfig(state);

            // Step 2: Run seeder (subprocess with real-time progress tracking)
            InstallProgress.Step = "migrations";
            InstallProgress.Message = "Starting database setup...";
            var (success, output) = await RunDbSeederWithProgressAsync(state);
            if (!success)
            {
                InstallProgress.HasError = true;
                InstallProgress.ErrorMessage = output;
                InstallProgress.Step = "error";
                InstallProgress.IsRunning = false;
                return;
            }

            // Brief pause so the polling client catches the final seeder step (e.g. "avatars")
            // before we overwrite it. The poll interval is 1.5s, so 2.5s guarantees at least one cycle.
            await Task.Delay(2500);

            // Step 3: Generate JWT for auto-login (before marking complete, so services don't restart yet)
            InstallProgress.Step = "finalizing";
            InstallProgress.Message = "Generating authentication token...";
            var jwt = await GenerateAdminJwtAsync(state);
            InstallProgress.Jwt = jwt;

            // Write setup-complete marker so Docker entrypoint knows install is done
            // (snakk-config.json is written in step 1, but entrypoint must wait for
            // the full install — migrations, seeding, JWT — before stopping the wizard)
            var completeMarker = Path.Combine(state.AvatarStoragePath, "conf", "setup-complete");
            File.WriteAllText(completeMarker, DateTime.UtcNow.ToString("O"));

            InstallProgress.Step = "complete";
            InstallProgress.Message = "Installation complete!";
            InstallProgress.IsRunning = false;
        }
        catch (Exception ex)
        {
            InstallProgress.HasError = true;
            InstallProgress.ErrorMessage = ex.Message;
            InstallProgress.Step = "error";
            InstallProgress.IsRunning = false;
        }
    }

    private async Task<(bool Success, string Output)> RunDbSeederWithProgressAsync(SetupState state)
    {
        var seederPath = FindDbSeederPath();
        if (seederPath is null)
            return (false, "Could not find Snakk.DbSeeder.dll. Ensure it's published.");

        var skipSeedFlag = state.SeedTestData ? "" : "--skip-seed";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{seederPath}\" {skipSeedFlag}".Trim(),
            WorkingDirectory = Path.GetDirectoryName(seederPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["ConnectionStrings__DbConnection"] = state.GetConnectionString(),
                ["FileStorage__BasePath"] = state.AvatarStoragePath,
                ["FileStorage__Provider"] = state.StorageProvider == "S3" ? "S3" : "",
                ["FileStorage__S3__Endpoint"] = state.S3Endpoint,
                ["FileStorage__S3__AccessKey"] = state.S3AccessKey,
                ["FileStorage__S3__SecretKey"] = state.S3SecretKey,
                ["FileStorage__S3__BucketName"] = state.S3BucketName,
                ["FileStorage__S3__PublicUrlBase"] = state.S3PublicUrlBase,
                ["Setup__AdminEmail"] = state.AdminEmail,
                ["Setup__AdminPassword"] = state.AdminPassword,
                ["Setup__AdminDisplayName"] = state.AdminDisplayName,
                ["Setup__CommunityName"] = state.CommunityName,
                ["Setup__CommunityDescription"] = state.CommunityDescription,
                ["Setup__FirstHubName"] = state.FirstHubName,
                ["Setup__FirstHubSlug"] = state.FirstHubSlug,
                ["Setup__FirstSpaceName"] = state.FirstSpaceName,
                ["Setup__FirstSpaceSlug"] = state.FirstSpaceSlug,
                ["Setup__CreateFirstCommunity"] = state.CreateFirstCommunity.ToString(),
                ["Snakk__SiteTimezone"] = state.Timezone,
                ["Setup__AllowedDisplayNameScripts"] = string.Join(",", state.AllowedDisplayNameScripts)
            }
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return (false, "Failed to start DbSeeder process.");

            var outputLines = new List<string>();

            // Read stdout line-by-line for real-time progress updates
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                outputLines.Add(line);

                // Only show meaningful lines as progress messages (skip raw SQL, blank lines, etc.)
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("RETURNING", StringComparison.Ordinal)
                                       && !trimmed.StartsWith("SELECT", StringComparison.Ordinal)
                                       && !trimmed.StartsWith("INSERT", StringComparison.Ordinal)
                                       && !trimmed.StartsWith("UPDATE", StringComparison.Ordinal)
                                       && !trimmed.StartsWith("DELETE", StringComparison.Ordinal))
                {
                    InstallProgress.Message = line;
                }

                // Parse progress markers from seeder output (steps are sequential, never go backwards)
                if (line.Contains("Applying pending migrations", StringComparison.OrdinalIgnoreCase))
                    InstallProgress.Step = "migrations";
                else if (line.Contains("admin", StringComparison.OrdinalIgnoreCase)
                         && (line.Contains("created", StringComparison.OrdinalIgnoreCase)
                             || line.Contains("creating", StringComparison.OrdinalIgnoreCase)
                             || line.Contains("assigned", StringComparison.OrdinalIgnoreCase)))
                    InstallProgress.Step = "admin";
                else if (line.Contains("database seeding", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("Clearing existing", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("discussions in", StringComparison.OrdinalIgnoreCase))
                    InstallProgress.Step = "seeding";
                else if (line.Contains("Generating avatars", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("Avatar generation complete", StringComparison.OrdinalIgnoreCase))
                    InstallProgress.Step = "avatars";
            }

            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = string.Join("\n", outputLines);
            if (!string.IsNullOrEmpty(errors))
                output += "\n" + errors;

            return (process.ExitCode == 0, output);
        }
        catch (Exception ex)
        {
            return (false, $"Error running DbSeeder: {ex.Message}");
        }
    }

    private static Dictionary<string, object> BuildFileStorageConfig(SetupState state)
    {
        var config = new Dictionary<string, object>
        {
            ["BasePath"] = state.AvatarStoragePath,
            ["PublicUrlBase"] = "/avatars"
        };

        if (state.StorageProvider == "S3")
        {
            config["Provider"] = "S3";
            config["S3"] = new Dictionary<string, string>
            {
                ["Endpoint"] = state.S3Endpoint,
                ["AccessKey"] = state.S3AccessKey,
                ["SecretKey"] = state.S3SecretKey,
                ["BucketName"] = state.S3BucketName,
                ["PublicUrlBase"] = state.S3PublicUrlBase
            };
        }

        return config;
    }

    private static bool IsLocalDomain(string domain) =>
        domain.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        domain.StartsWith("127.", StringComparison.Ordinal) ||
        domain.StartsWith("192.168.", StringComparison.Ordinal) ||
        domain.StartsWith("10.", StringComparison.Ordinal);
}

/// <summary>
/// Static progress tracker for the background installation process.
/// </summary>
public static class InstallProgress
{
    private static Timer? _jwtExpiryTimer;

    public static string Step { get; set; } = "idle";
    public static string? Message { get; set; }
    public static bool IsRunning { get; set; }
    public static bool HasError { get; set; }
    public static string? ErrorMessage { get; set; }
    public static bool SeedEnabled { get; set; }

    private static string? _jwt;
    public static string? Jwt
    {
        get => _jwt;
        set
        {
            _jwt = value;
            _jwtExpiryTimer?.Dispose();
            _jwtExpiryTimer = null;

            if (value is not null)
            {
                // Auto-clear JWT from memory after 5 minutes — if OnPostFinalize hasn't consumed it by then,
                // the setup window has closed and keeping the token in static memory is a security risk.
                _jwtExpiryTimer = new Timer(_ => { _jwt = null; _jwtExpiryTimer?.Dispose(); _jwtExpiryTimer = null; },
                    null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
            }
        }
    }

    public static void Reset()
    {
        Step = "idle";
        Message = null;
        IsRunning = false;
        HasError = false;
        ErrorMessage = null;
        Jwt = null;
        SeedEnabled = false;
    }
}
