using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Snakk.Infrastructure.Database;

namespace Snakk.Api.Tests.Helpers;

/// <summary>
/// Test web server that wraps WebApplicationFactory to configure the API
/// with an InMemory database and known test JWT settings.
/// Each instance gets a unique database to ensure test isolation.
/// </summary>
public class TestWebServer : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"SnakkTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test-specific configuration with known JWT secret
            var testConfig = new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = AuthHelper.TestJwtSecret,
                ["Jwt:Issuer"] = AuthHelper.TestJwtIssuer,
                ["Jwt:Audience"] = AuthHelper.TestJwtAudience,
                ["Jwt:ExpirationMinutes"] = "60",
                ["ConnectionStrings:DbConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["FileStorage:BasePath"] = Path.GetTempPath(),
                ["Realtime:BaseUrl"] = "http://localhost:15300",
                ["Realtime:ApiKey"] = "test-api-key"
            };
            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations
            var dbContextDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("SnakkDbContext") == true
                         || d.ServiceType.FullName?.Contains("DbContextOptions") == true
                         || d.ServiceType.FullName?.Contains("DbContextPool") == true
                         || d.ServiceType.FullName?.Contains("IDbContextFactory") == true)
                .ToList();
            foreach (var descriptor in dbContextDescriptors)
                services.Remove(descriptor);

            // Also remove by concrete types
            services.RemoveAll<DbContextOptions<SnakkDbContext>>();
            services.RemoveAll<SnakkDbContext>();

            // Add InMemory database with a unique name per test server instance
            services.AddDbContext<SnakkDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Services added in production now inject IDbContextFactory<SnakkDbContext>
            // (e.g. CounterService, PermissionService). Provide an InMemory factory that
            // shares the same database name so both paths see the same data.
            services.AddSingleton<IDbContextFactory<SnakkDbContext>>(
                new TestDbContextFactory(_databaseName));

            // DataProtectionDbContext was added by commit 0af6027 (DataProtection keys in
            // Postgres) but the production registration uses UseNpgsql, which the InMemory
            // test provider rejects. Re-register against the same InMemory store so the
            // Program.cs startup that resolves it during EnsureSchemaAsync succeeds.
            services.AddDbContext<DataProtectionDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName + "_dp"));

            // Remove the health check that requires a real DB connection
            var healthCheckDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("HealthCheck") == true).ToList();
            foreach (var descriptor in healthCheckDescriptors)
                services.Remove(descriptor);

            services.AddHealthChecks();
        });
    }

    /// <summary>
    /// Creates an HttpClient with an Authorization header set using a test JWT token.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        string userId = "test-user-id",
        string displayName = "Test User",
        string? email = "test@example.com",
        bool emailVerified = true,
        string? role = null)
    {
        var client = CreateClient();
        var token = AuthHelper.GenerateTestToken(userId, displayName, email, emailVerified, role);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private sealed class TestDbContextFactory(string dbName) : IDbContextFactory<SnakkDbContext>
    {
        public SnakkDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<SnakkDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new SnakkDbContext(options);
        }
    }
}
