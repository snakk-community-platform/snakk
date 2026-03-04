using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Snakk.Protos.Auth;
using Snakk.Web.Services;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory for Snakk.Web integration tests.
/// Replaces the real SnakkApiClient with a Moq mock so BFF endpoints
/// can be tested without a running Snakk.Api gRPC instance.
/// Also stubs out gRPC channels, community resolution, and the setup wizard.
/// </summary>
public class TestWebApp : WebApplicationFactory<Program>
{
    /// <summary>
    /// Moq mock for SnakkApiClient. Configure return values before making test requests.
    /// </summary>
    public Mock<SnakkApiClient> MockApiClient { get; }

    /// <summary>
    /// Moq mock for AuthServiceClient (used directly by RefreshToken endpoint).
    /// </summary>
    public Mock<AuthService.AuthServiceClient> MockAuthClient { get; }

    /// <summary>
    /// HTTP mock handler for tests that use direct HttpClient (e.g., ManageScopeService).
    /// </summary>
    public MockApiHandler MockApiHandler { get; } = new();

    public TestWebApp()
    {
        // Create dummy gRPC channel — only needed for SnakkApiClient constructor param types
        var dummyChannel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:19999");

        MockApiClient = new Mock<SnakkApiClient>(
            MockBehavior.Loose,
            new Snakk.Protos.Community.CommunityService.CommunityServiceClient(dummyChannel),
            new Snakk.Protos.Hub.HubService.HubServiceClient(dummyChannel),
            new Snakk.Protos.Space.SpaceService.SpaceServiceClient(dummyChannel),
            new Snakk.Protos.Discussion.DiscussionService.DiscussionServiceClient(dummyChannel),
            new Snakk.Protos.Post.PostService.PostServiceClient(dummyChannel),
            new Snakk.Protos.Follow.FollowService.FollowServiceClient(dummyChannel),
            new Snakk.Protos.Reaction.ReactionService.ReactionServiceClient(dummyChannel),
            new Snakk.Protos.Notification.NotificationService.NotificationServiceClient(dummyChannel),
            new Snakk.Protos.Moderation.ModerationService.ModerationServiceClient(dummyChannel),
            new Snakk.Protos.Search.SearchService.SearchServiceClient(dummyChannel),
            new Snakk.Protos.Statistics.StatisticsService.StatisticsServiceClient(dummyChannel),
            new Snakk.Protos.User.UserService.UserServiceClient(dummyChannel),
            new Snakk.Protos.ReadState.ReadStateService.ReadStateServiceClient(dummyChannel),
            new Snakk.Protos.Markup.MarkupService.MarkupServiceClient(dummyChannel),
            new AuthService.AuthServiceClient(dummyChannel),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SnakkApiClient>.Instance);

        MockAuthClient = new Mock<AuthService.AuthServiceClient>(dummyChannel);
    }

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

        // Set API base URL to a dummy value (calls are intercepted by mocks)
        builder.UseSetting("ApiBaseUrl", "http://localhost:19999");

        // Set a default community slug
        builder.UseSetting("Snakk:DefaultCommunitySlug", "main");
        builder.UseSetting("Snakk:PrimaryDomains:0", "localhost");

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace SnakkApiClient with mock
            services.RemoveAll<SnakkApiClient>();
            services.AddSingleton(MockApiClient.Object);

            // Replace AuthServiceClient with mock (used by RefreshToken endpoint)
            services.RemoveAll<AuthService.AuthServiceClient>();
            services.AddSingleton(MockAuthClient.Object);

            // Replace gRPC channel with a dummy (prevents connection attempts to real API)
            var dummyChannel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:19999");
            services.RemoveAll<Grpc.Net.Client.GrpcChannel>();
            services.AddSingleton(_ => dummyChannel);

            // Replace the "InternalApi" named HttpClient factory with one that uses MockApiHandler
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(sp =>
                new MockHttpClientFactory(MockApiHandler));

            // Replace the domain cache service with a simple stub that always returns "not found"
            services.RemoveAll<ICommunityDomainCacheService>();
            services.AddSingleton<ICommunityDomainCacheService, StubCommunityDomainCacheService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            MockApiHandler.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Simple IHttpClientFactory that always returns an HttpClient backed by the MockApiHandler.
/// </summary>
internal class MockHttpClientFactory(MockApiHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient(handler, disposeHandler: false)
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
