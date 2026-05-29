using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

/// <summary>
/// Regression coverage for the stored-XSS path closed by
/// fix/post-topic-item-stored-xss. The reactions / saved-posts projections used to
/// emit the raw markdown source (`Post.Content.Substring(0, 300)`) as the excerpt,
/// which `_PostTopicItem.cshtml` then rendered with `@Html.Raw` — so a post body
/// containing &lt;script&gt; reached the DOM of every viewer's saved/reactions page.
/// The fix prefers `Post.PlainTextExcerpt` (markdown-stripped on save) and the
/// template now auto-encodes.
/// </summary>
public class PostExcerptProjectionTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ReactionQueryRepository _reactions;
    private readonly SaveRepository _saves;

    public PostExcerptProjectionTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _reactions = new ReactionQueryRepository(_db.Context);
        _saves = new SaveRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    private const string MaliciousContent = "<script>alert(document.cookie)</script>";
    private const string CleanExcerpt = "Stripped plain-text excerpt for the post.";

    [Test]
    public async Task GetReactedPostsByUser_PrefersPlainTextExcerpt_NotRawContent()
    {
        // CreateFullHierarchyAsync produces the first post in the discussion. The
        // post-reactions feed filters to non-first posts (the discussion-reactions
        // feed handles first posts), so create a reply to react to.
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        var reply = await _builder.CreatePostAsync(discussion.Id, user.Id);
        reply.Content = MaliciousContent;
        reply.PlainTextExcerpt = CleanExcerpt;
        await _db.Context.PostReactions.AddAsync(new PostReactionDatabaseEntity
        {
            PublicId = $"reaction_{Guid.NewGuid():N}",
            UserId = user.Id,
            UserPublicId = user.PublicId,
            PostId = reply.Id,
            TypeId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await _db.Context.SaveChangesAsync();

        var page = await _reactions.GetReactedPostsByUserAsync(user.PublicId!, offset: 0, pageSize: 10);

        var items = page.Items.ToList();
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].ContentExcerpt).IsEqualTo(CleanExcerpt);
        await Assert.That(items[0].ContentExcerpt).DoesNotContain("<script>");
    }

    [Test]
    public async Task GetReactedPostsByUser_LegacyPostWithoutExcerpt_FallsBackToRawSubstring()
    {
        // Legacy rows (pre-PlainTextExcerpt) still need a value. The fallback returns
        // the raw substring; safety here depends on the consuming template auto-encoding
        // (which it now does). The point of THIS test is "no NullReferenceException
        // when PlainTextExcerpt is missing".
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        var reply = await _builder.CreatePostAsync(discussion.Id, user.Id, content: "Legacy content with **markdown** and <b>HTML</b>");
        reply.PlainTextExcerpt = null;
        await _db.Context.PostReactions.AddAsync(new PostReactionDatabaseEntity
        {
            PublicId = $"reaction_{Guid.NewGuid():N}",
            UserId = user.Id,
            UserPublicId = user.PublicId,
            PostId = reply.Id,
            TypeId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await _db.Context.SaveChangesAsync();

        var page = await _reactions.GetReactedPostsByUserAsync(user.PublicId!, offset: 0, pageSize: 10);

        var items = page.Items.ToList();
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].ContentExcerpt).IsEqualTo(reply.Content);
    }

    [Test]
    public async Task GetSavedPostsByUser_PrefersPlainTextExcerpt_NotRawContent()
    {
        var (user, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        post.Content = MaliciousContent;
        post.PlainTextExcerpt = CleanExcerpt;
        await _db.Context.UserSaves.AddAsync(new UserSaveDatabaseEntity
        {
            PublicId = $"save_{Guid.NewGuid():N}",
            UserId = user.Id,
            PostId = post.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.Context.SaveChangesAsync();

        var page = await _saves.GetSavedPostsAsync(user.PublicId!, offset: 0, pageSize: 10);

        var items = page.Items.ToList();
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].ContentExcerpt).IsEqualTo(CleanExcerpt);
        await Assert.That(items[0].ContentExcerpt).DoesNotContain("<script>");
    }
}
