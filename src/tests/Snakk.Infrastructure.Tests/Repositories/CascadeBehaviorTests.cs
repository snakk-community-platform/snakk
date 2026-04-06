using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class CascadeBehaviorTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;

    public CascadeBehaviorTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    [Test]
    public async Task DeleteCommunityWithHubs_ThrowsDueToRestrict()
    {
        var community = await _builder.CreateCommunityAsync();
        await _builder.CreateHubAsync(community.Id);

        // Use a separate context so EF does not track the child hub entity,
        // which would cause an InvalidOperationException at Remove() time
        // rather than letting the database enforce the FK constraint.
        using var deleteContext = _db.CreateSeparateContext();
        var communityToDelete = await deleteContext.Communities
            .FirstAsync(c => c.Id == community.Id);

        deleteContext.Communities.Remove(communityToDelete);

        var act = async () => { await deleteContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DeleteHubWithSpaces_ThrowsDueToRestrict()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        await _builder.CreateSpaceAsync(hub.Id);

        using var deleteContext = _db.CreateSeparateContext();
        var hubToDelete = await deleteContext.Hubs
            .FirstAsync(h => h.Id == hub.Id);

        deleteContext.Hubs.Remove(hubToDelete);

        var act = async () => { await deleteContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DeleteSpaceWithDiscussions_ThrowsDueToRestrict()
    {
        var (user, community, hub, space, discussion, _) = await _builder.CreateFullHierarchyAsync();

        using var deleteContext = _db.CreateSeparateContext();
        var spaceToDelete = await deleteContext.Spaces
            .FirstAsync(s => s.Id == space.Id);

        deleteContext.Spaces.Remove(spaceToDelete);

        var act = async () => { await deleteContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DeleteDiscussionWithPosts_ThrowsDueToRestrict()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();

        using var deleteContext = _db.CreateSeparateContext();
        var discussionToDelete = await deleteContext.Discussions
            .FirstAsync(d => d.Id == discussion.Id);

        deleteContext.Discussions.Remove(discussionToDelete);

        var act = async () => { await deleteContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DeleteUser_CascadeDeletesUserRoles()
    {
        var admin = await _builder.CreateUserAsync(displayName: "Admin");
        var user = await _builder.CreateUserAsync(displayName: "Target User");
        var community = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(
            userId: user.Id,
            roleType: UserRoleTypeEnum.CommunityAdmin,
            assignedByUserId: admin.Id,
            communityId: community.Id);

        // Verify the role exists
        using var verifyContext = _db.CreateSeparateContext();
        var rolesBefore = await verifyContext.UserRoles
            .Where(r => r.UserId == user.Id)
            .ToListAsync();
        await Assert.That(rolesBefore.Count).IsEqualTo(1);

        // Delete the user
        _db.Context.Users.Remove(user);
        await _db.Context.SaveChangesAsync();

        // Verify that roles were cascade-deleted
        using var readContext = _db.CreateSeparateContext();
        var rolesAfter = await readContext.UserRoles
            .Where(r => r.UserId == user.Id)
            .ToListAsync();
        await Assert.That(rolesAfter.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteUser_CascadeDeletesRefreshTokens()
    {
        var user = await _builder.CreateUserAsync();

        var token = new RefreshTokenDatabaseEntity
        {
            PublicId = $"rt_{Guid.NewGuid():N}",
            TokenValue = $"token_{Guid.NewGuid():N}",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.RefreshTokens.Add(token);
        await _db.Context.SaveChangesAsync();

        // Verify the token exists
        using var verifyContext = _db.CreateSeparateContext();
        var tokensBefore = await verifyContext.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();
        await Assert.That(tokensBefore.Count).IsEqualTo(1);

        // Delete the user
        _db.Context.Users.Remove(user);
        await _db.Context.SaveChangesAsync();

        // Verify that refresh tokens were cascade-deleted
        using var readContext = _db.CreateSeparateContext();
        var tokensAfter = await readContext.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();
        await Assert.That(tokensAfter.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeletePost_CascadeDeletesReactions()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var reaction = new PostReactionDatabaseEntity
        {
            PublicId = $"react_{Guid.NewGuid():N}",
            PostId = post.Id,
            UserId = user.Id,
            TypeId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.PostReactions.Add(reaction);
        await _db.Context.SaveChangesAsync();

        // Verify the reaction exists
        using var verifyContext = _db.CreateSeparateContext();
        var reactionsBefore = await verifyContext.PostReactions
            .Where(r => r.PostId == post.Id)
            .ToListAsync();
        await Assert.That(reactionsBefore.Count).IsEqualTo(1);

        // Delete the post directly (Reaction->Post is Cascade, so this should work)
        _db.Context.Posts.Remove(post);
        await _db.Context.SaveChangesAsync();

        // Verify that reactions were cascade-deleted
        using var readContext = _db.CreateSeparateContext();
        var reactionsAfter = await readContext.PostReactions
            .Where(r => r.PostId == post.Id)
            .ToListAsync();
        await Assert.That(reactionsAfter.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteCommunityWithoutChildren_Succeeds()
    {
        var community = await _builder.CreateCommunityAsync();

        _db.Context.Communities.Remove(community);
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var communities = await readContext.Communities
            .IgnoreQueryFilters()
            .ToListAsync();
        await Assert.That(communities.Count).IsEqualTo(0);
    }
}
