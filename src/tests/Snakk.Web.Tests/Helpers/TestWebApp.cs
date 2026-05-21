using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Protos.Auth;
using Snakk.Web.Services;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory for Snakk.Web integration tests.
/// Replaces the real SnakkApiClient with an NSubstitute mock so BFF endpoints
/// can be tested without a running Snakk.Api gRPC instance.
/// Also stubs out gRPC channels, community resolution, and the setup wizard.
/// </summary>
public class TestWebApp : WebApplicationFactory<Program>
{
    /// <summary>
    /// NSubstitute mock for SnakkApiClient. Configure return values before making test requests.
    /// </summary>
    public SnakkApiClient MockApiClient { get; }

    /// <summary>
    /// NSubstitute mock for AuthServiceClient (used directly by RefreshToken endpoint).
    /// </summary>
    public AuthService.AuthServiceClient MockAuthClient { get; }

    /// <summary>
    /// HTTP mock handler for tests that use direct HttpClient (e.g., ManageScopeService).
    /// </summary>
    public MockApiHandler MockApiHandler { get; } = new();

    public TestWebApp()
    {
        MockApiClient = Substitute.For<SnakkApiClient>(
            Substitute.For<Snakk.Protos.Community.CommunityService.CommunityServiceClient>(),
            Substitute.For<Snakk.Protos.Hub.HubService.HubServiceClient>(),
            Substitute.For<Snakk.Protos.Space.SpaceService.SpaceServiceClient>(),
            Substitute.For<Snakk.Protos.Discussion.DiscussionService.DiscussionServiceClient>(),
            Substitute.For<Snakk.Protos.Post.PostService.PostServiceClient>(),
            Substitute.For<Snakk.Protos.Follow.FollowService.FollowServiceClient>(),
            Substitute.For<Snakk.Protos.Reaction.ReactionService.ReactionServiceClient>(),
            Substitute.For<Snakk.Protos.Notification.NotificationService.NotificationServiceClient>(),
            Substitute.For<Snakk.Protos.Moderation.ModerationService.ModerationServiceClient>(),
            Substitute.For<Snakk.Protos.Search.SearchService.SearchServiceClient>(),
            Substitute.For<Snakk.Protos.Statistics.StatisticsService.StatisticsServiceClient>(),
            Substitute.For<Snakk.Protos.User.UserService.UserServiceClient>(),
            Substitute.For<Snakk.Protos.ReadState.ReadStateService.ReadStateServiceClient>(),
            Substitute.For<Snakk.Protos.Markup.MarkupService.MarkupServiceClient>(),
            Substitute.For<AuthService.AuthServiceClient>(),
            Substitute.For<Snakk.Protos.Banner.BannerService.BannerServiceClient>(),
            Substitute.For<Snakk.Protos.Consent.ConsentService.ConsentServiceClient>(),
            Substitute.For<Snakk.Protos.Save.SaveService.SaveServiceClient>(),
            Substitute.For<ILogger<SnakkApiClient>>());
        MockAuthClient = Substitute.For<AuthService.AuthServiceClient>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Skip frontend npm build in test mode
        builder.UseSetting("SkipFrontendBuild", "true");

        // Ensure the setup wizard thinks setup is complete so it doesn't redirect
        // (setup completion is detected by the presence of conf/snakk-config.json)
        var tempStorage = Path.Combine(Path.GetTempPath(), "snakk-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempStorage, "conf"));
        File.WriteAllText(Path.Combine(tempStorage, "conf", "snakk-config.json"), "{}");

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
            services.AddSingleton(MockApiClient);

            // Replace AuthServiceClient with mock (used by RefreshToken endpoint)
            services.RemoveAll<AuthService.AuthServiceClient>();
            services.AddSingleton(MockAuthClient);

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

    public Task InvalidateDomainAsync(string domain) => Task.CompletedTask;
}
