using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class ReportReasonRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ReportReasonRepository _repository;

    public ReportReasonRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new ReportReasonRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingReason_ReturnsEntity()
    {
        var reason = await _builder.CreateReportReasonAsync("Spam", "Unsolicited advertising");

        var result = await _repository.GetByPublicIdAsync(reason.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(reason.PublicId);
        await Assert.That(result.Name).IsEqualTo("Spam");
        await Assert.That(result.Description).IsEqualTo("Unsolicited advertising");
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetReasonsForScopeAsync Tests

    [Test]
    public async Task GetReasonsForScopeAsync_WithCommunityId_ReturnsGlobalAndCommunityReasons()
    {
        var community = await _builder.CreateCommunityAsync();

        // Global reasons
        var globalReason = await _builder.CreateReportReasonAsync("Spam");
        var globalReason2 = await _builder.CreateReportReasonAsync("Harassment");

        // Community-level reason
        var communityReason = await _builder.CreateReportReasonAsync("Community Rule");
        communityReason.CommunityId = community.Id;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetReasonsForScopeAsync(communityId: community.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetReasonsForScopeAsync_WithNoParams_ReturnsOnlyGlobalReasons()
    {
        var community = await _builder.CreateCommunityAsync();

        var globalReason = await _builder.CreateReportReasonAsync("Spam");

        var communityReason = await _builder.CreateReportReasonAsync("Community Rule");
        communityReason.CommunityId = community.Id;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetReasonsForScopeAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("Spam");
    }

    #endregion

    #region GetGlobalReasonsAsync Tests

    [Test]
    public async Task GetGlobalReasonsAsync_ReturnsOnlyGlobalReasons()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Global reasons
        var globalReason1 = await _builder.CreateReportReasonAsync("Spam");
        var globalReason2 = await _builder.CreateReportReasonAsync("Harassment");

        // Community-level reason
        var communityReason = await _builder.CreateReportReasonAsync("Community Rule");
        communityReason.CommunityId = community.Id;

        // Hub-level reason
        var hubReason = await _builder.CreateReportReasonAsync("Hub Rule");
        hubReason.HubId = hub.Id;

        // Space-level reason
        var spaceReason = await _builder.CreateReportReasonAsync("Space Rule");
        spaceReason.SpaceId = space.Id;

        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetGlobalReasonsAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        var names = result.Select(r => r.Name).ToList();
        await Assert.That(names).Contains("Spam");
        await Assert.That(names).Contains("Harassment");
    }

    #endregion

    #region GetReasonsByEntityAsync Tests

    [Test]
    public async Task GetReasonsByEntityAsync_WithSpaceId_ReturnsOnlySpaceLevelReasons()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Global reason
        await _builder.CreateReportReasonAsync("Spam");

        // Community-level reason
        var communityReason = await _builder.CreateReportReasonAsync("Community Rule");
        communityReason.CommunityId = community.Id;

        // Space-level reason
        var spaceReason = await _builder.CreateReportReasonAsync("Space Rule");
        spaceReason.SpaceId = space.Id;

        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetReasonsByEntityAsync(spaceId: space.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("Space Rule");
    }

    [Test]
    public async Task GetReasonsByEntityAsync_WithCommunityId_ReturnsOnlyCommunityLevelReasons()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        // Global reason
        await _builder.CreateReportReasonAsync("Spam");

        // Community-level reason
        var communityReason = await _builder.CreateReportReasonAsync("Community Rule");
        communityReason.CommunityId = community.Id;

        // Hub-level reason
        var hubReason = await _builder.CreateReportReasonAsync("Hub Rule");
        hubReason.HubId = hub.Id;

        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetReasonsByEntityAsync(communityId: community.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("Community Rule");
    }

    [Test]
    public async Task GetReasonsByEntityAsync_WithNoParams_ReturnsGlobalReasons()
    {
        var community = await _builder.CreateCommunityAsync();

        var globalReason = await _builder.CreateReportReasonAsync("Spam");
        var globalReason2 = await _builder.CreateReportReasonAsync("Harassment");

        var communityReason = await _builder.CreateReportReasonAsync("Community Rule");
        communityReason.CommunityId = community.Id;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetReasonsByEntityAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        var names = result.Select(r => r.Name).ToList();
        await Assert.That(names).Contains("Spam");
        await Assert.That(names).Contains("Harassment");
    }

    #endregion
}
