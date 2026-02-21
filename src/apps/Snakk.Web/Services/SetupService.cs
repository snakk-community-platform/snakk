using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Snakk.Web.Services;

/// <summary>
/// Handles setup wizard operations: DB testing, config writing, DbSeeder invocation.
/// </summary>
public class SetupService
{
    private readonly IConfiguration _configuration;

    public SetupService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

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
    /// Write the appsettings.Production.json file with all setup values.
    /// </summary>
    public void WriteProductionConfig(SetupState state)
    {
        var config = new Dictionary<string, object>
        {
            ["ConnectionStrings"] = new Dictionary<string, string>
            {
                ["DbConnection"] = state.GetConnectionString()
            },
            ["Jwt"] = new Dictionary<string, object>
            {
                ["SecretKey"] = state.JwtSecretKey,
                ["Issuer"] = "Snakk",
                ["Audience"] = "Snakk"
            },
            ["Realtime"] = new Dictionary<string, string>
            {
                ["ApiKey"] = state.RealtimeApiKey
            },
            ["Snakk"] = new Dictionary<string, object>
            {
                ["Domain"] = state.Domain,
                ["SiteName"] = state.SiteName,
                ["DefaultCommunitySlug"] = state.DefaultCommunitySlug,
                ["PrimaryDomains"] = new[] { state.Domain }
            },
            ["Features"] = new Dictionary<string, object>
            {
                ["MultiCommunityEnabled"] = state.MultiCommunityEnabled
            },
            ["FileStorage"] = new Dictionary<string, string>
            {
                ["BasePath"] = state.AvatarStoragePath,
                ["PublicUrlBase"] = "/avatars"
            },
            ["Setup"] = new Dictionary<string, string>
            {
                ["AdminEmail"] = state.AdminEmail,
                ["AdminPassword"] = state.AdminPassword,
                ["AdminDisplayName"] = state.AdminDisplayName
            },
            ["Cors"] = new Dictionary<string, string>
            {
                ["AllowedOrigins"] = $"https://{state.Domain}"
            }
        };

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
        {
            config["Authentication"] = auth;
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var configPath = Path.Combine(state.AvatarStoragePath, "appsettings.Production.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// Run the DbSeeder tool as a subprocess.
    /// Returns (success, output).
    /// </summary>
    public async Task<(bool Success, string Output)> RunDbSeederAsync(SetupState state)
    {
        // Find DbSeeder — in Docker it's at /app/dbseeder/, locally it's relative
        var seederPath = FindDbSeederPath();
        if (seederPath == null)
        {
            return (false, "Could not find Snakk.DbSeeder.dll. Ensure it's published.");
        }

        var skipSeedFlag = state.SeedTestData ? "" : "--skip-seed";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{seederPath}\" {skipSeedFlag}".Trim(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["ConnectionStrings__DbConnection"] = state.GetConnectionString(),
                ["FileStorage__BasePath"] = state.AvatarStoragePath,
                ["Setup__AdminEmail"] = state.AdminEmail,
                ["Setup__AdminPassword"] = state.AdminPassword,
                ["Setup__AdminDisplayName"] = state.AdminDisplayName
            }
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return (false, "Failed to start DbSeeder process.");

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
    /// Create the .setup-complete marker file.
    /// </summary>
    public void MarkSetupComplete(string storagePath)
    {
        var markerPath = Path.Combine(storagePath, ".setup-complete");
        File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Generate a JWT for auto-login as the admin user after setup.
    /// Queries the database for the admin's PublicId, then creates a token
    /// matching the format used by TokenService.
    /// </summary>
    public async Task<string> GenerateAdminJwtAsync(SetupState state)
    {
        // Query the newly created admin user from the database to get their PublicId
        await using var conn = new Npgsql.NpgsqlConnection(state.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"PublicId\" FROM \"Users\" WHERE \"Email\" = @email LIMIT 1";
        cmd.Parameters.AddWithValue("email", state.AdminEmail);
        var result = await cmd.ExecuteScalarAsync();
        var publicId = result?.ToString()
            ?? throw new InvalidOperationException("Admin user was not created in the database.");

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
}
