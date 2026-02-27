using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Snakk.Web.Services;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory for Snakk.Web integration tests.
/// Replaces the real API HttpClient with a MockApiHandler so BFF endpoints
/// can be tested without a running Snakk.Api instance.
/// Also stubs out gRPC channels, community resolution, and the setup wizard.
/// </summary>
public class TestWebApp : WebApplicationFactory<Program>
{
    /// <summary>
    /// The mock handler that intercepts all outgoing API calls from BFF endpoints.
    /// Configure responses on this handler before making test requests.
    /// </summary>
    public MockApiHandler MockApiHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Skip Tailwind CSS npm build in test mode
        builder.UseSetting("SkipBuildTailwindCSS", "true");

        // Ensure the setup wizard thinks setup is complete so it doesn't redirect
        var tempStorage = Path.Combine(Path.GetTempPath(), "snakk-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempStorage);
        File.WriteAllText(Path.Combine(tempStorage, ".setup-complete"), "test");

        builder.UseSetting("FileStorage:BasePath", tempStorage);

        // Set a known JWT secret for test token generation
        builder.UseSetting("Jwt:SecretKey", TestJwtHelper.TestSecretKey);
        builder.UseSetting("Jwt:Issuer", "Snakk");
        builder.UseSetting("Jwt:Audience", "Snakk");

        // Set API base URL to a dummy value (all calls are intercepted by MockApiHandler)
        builder.UseSetting("ApiBaseUrl", "http://localhost:19999");

        // Set a default community slug
        builder.UseSetting("Snakk:DefaultCommunitySlug", "main");
        builder.UseSetting("Snakk:PrimaryDomains:0", "localhost");

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace the named HttpClient for SnakkApiClient with our mock handler.
            // Remove the existing SnakkApiClient registration and add our own.
            services.RemoveAll<SnakkApiClient>();
            services.AddTransient(_ =>
            {
                var httpClient = new HttpClient(MockApiHandler)
                {
                    BaseAddress = new Uri("http://localhost:19999")
                };
                return new SnakkApiClient(httpClient);
            });

            // Replace the "InternalApi" named HttpClient factory with one that uses MockApiHandler
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(sp =>
                new MockHttpClientFactory(MockApiHandler));

            // Replace gRPC channel with a dummy (prevents connection attempts to real API)
            services.RemoveAll<Grpc.Net.Client.GrpcChannel>();
            services.AddSingleton(_ =>
                Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:19999"));

            // Replace gRPC AuthServiceClient with a mock that won't be called
            // (BFF auth endpoints use HTTP, not gRPC, for most operations)
            services.RemoveAll<Snakk.Protos.Auth.AuthService.AuthServiceClient>();
            services.AddScoped(_ =>
                new Snakk.Protos.Auth.AuthService.AuthServiceClient(
                    Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:19999")));

            // Replace the domain cache service with a simple stub that always returns "not found"
            services.RemoveAll<ICommunityDomainCacheService>();
            services.AddSingleton<ICommunityDomainCacheService, StubCommunityDomainCacheService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MockApiHandler.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Simple IHttpClientFactory that always returns an HttpClient backed by the MockApiHandler.
/// </summary>
internal class MockHttpClientFactory : IHttpClientFactory
{
    private readonly MockApiHandler _handler;

    public MockHttpClientFactory(MockApiHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost:19999")
        };
    }
}

/// <summary>
/// Stub community domain cache that always returns "not found" — disables custom domain resolution.
/// This prevents the middleware from making real API calls during tests.
/// </summary>
internal class StubCommunityDomainCacheService : ICommunityDomainCacheService
{
    public Task<CommunityDomainLookupResult> GetCommunitySlugForDomainAsync(string domain)
    {
        return Task.FromResult(new CommunityDomainLookupResult(false, null));
    }

    public void InvalidateDomain(string domain) { }
}
