using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Tests for SnakkUrlHelper static methods which generate community-aware URLs.
/// Uses real CommunityContext instances to test URL prefix logic.
///
/// URL rules:
/// - IsMultiCommunityEnabled = false (single-community): flat URLs, no /c/ prefix
/// - IsMultiCommunityEnabled = true (multi-community): always /c/{slug} prefix
/// - Custom domain: never gets /c/{slug} prefix (domain identifies community)
/// </summary>
public class SnakkUrlHelperTests
{
    private ICommunityContext CreateContext(
        string slug = "test-community",
        bool isCustomDomain = false,
        bool isMultiCommunity = false)
    {
        var context = new CommunityContext();
        context.SetCommunity(slug, isCustomDomain, "Test Community", isMultiCommunity);
        return context;
    }

    // ===== Community Prefix Tests =====

    [Test]
    public async Task CommunityPrefix_SingleCommunity_ReturnsEmpty()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CommunityPrefix_CustomDomain_ReturnsEmpty()
    {
        var context = CreateContext(isCustomDomain: true, isMultiCommunity: true);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CommunityPrefix_MultiCommunity_ReturnsCSlug()
    {
        var context = CreateContext(slug: "snakk", isMultiCommunity: true);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("/c/snakk");
    }

    // ===== Hub URL Tests =====

    [Test]
    public async Task Hub_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.Hub(context, "tech");
        await Assert.That(result).IsEqualTo("/h/tech");
    }

    [Test]
    public async Task Hub_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "gaming", isMultiCommunity: true);
        var result = SnakkUrlHelper.Hub(context, "fps");
        await Assert.That(result).IsEqualTo("/c/gaming/h/fps");
    }

    [Test]
    public async Task Hub_ExplicitSlug_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "main", isMultiCommunity: true);
        var result = SnakkUrlHelper.Hub("other-community", context, "my-hub");
        await Assert.That(result).IsEqualTo("/c/other-community/h/my-hub");
    }

    [Test]
    public async Task Hub_ExplicitSlug_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.Hub("main", context, "my-hub");
        await Assert.That(result).IsEqualTo("/h/my-hub");
    }

    // ===== Space URL Tests =====

    [Test]
    public async Task Space_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.Space(context, "tech", "csharp");
        await Assert.That(result).IsEqualTo("/h/tech/csharp");
    }

    [Test]
    public async Task Space_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "gaming", isMultiCommunity: true);
        var result = SnakkUrlHelper.Space(context, "fps", "valorant");
        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/valorant");
    }

    [Test]
    public async Task Space_ExplicitSlug_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "main", isMultiCommunity: true);
        var result = SnakkUrlHelper.Space("other-community", context, "my-hub", "my-space");
        await Assert.That(result).IsEqualTo("/c/other-community/h/my-hub/my-space");
    }

    [Test]
    public async Task Space_ExplicitSlug_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.Space("main", context, "my-hub", "my-space");
        await Assert.That(result).IsEqualTo("/h/my-hub/my-space");
    }

    // ===== Discussion URL Tests =====

    [Test]
    public async Task Discussion_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.Discussion(context, "tech", "csharp", "my-thread-abc123");
        await Assert.That(result).IsEqualTo("/h/tech/csharp/my-thread-abc123");
    }

    [Test]
    public async Task Discussion_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "gaming", isMultiCommunity: true);
        var result = SnakkUrlHelper.Discussion(context, "fps", "valorant", "patch-notes-xyz789");
        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/valorant/patch-notes-xyz789");
    }

    [Test]
    public async Task Discussion_ExplicitSlug_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "main", isMultiCommunity: true);
        var result = SnakkUrlHelper.Discussion("other-community", context, "tech", "csharp", "my-thread-abc123");
        await Assert.That(result).IsEqualTo("/c/other-community/h/tech/csharp/my-thread-abc123");
    }

    // ===== Manage URL Tests =====

    [Test]
    public async Task ManageCommunity_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(slug: "main", isMultiCommunity: false);
        var result = SnakkUrlHelper.ManageCommunity(context);
        await Assert.That(result).IsEqualTo("/admin");
    }

    [Test]
    public async Task ManageCommunity_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "test-community", isMultiCommunity: true);
        var result = SnakkUrlHelper.ManageCommunity(context);
        await Assert.That(result).IsEqualTo("/admin/c/test-community");
    }

    [Test]
    public async Task ManageHub_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(slug: "main", isMultiCommunity: false);
        var result = SnakkUrlHelper.ManageHub(context, "my-hub");
        await Assert.That(result).IsEqualTo("/admin/h/my-hub");
    }

    [Test]
    public async Task ManageHub_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "test-community", isMultiCommunity: true);
        var result = SnakkUrlHelper.ManageHub(context, "my-hub");
        await Assert.That(result).IsEqualTo("/admin/c/test-community/h/my-hub");
    }

    [Test]
    public async Task ManageSpace_SingleCommunity_OmitsPrefix()
    {
        var context = CreateContext(slug: "main", isMultiCommunity: false);
        var result = SnakkUrlHelper.ManageSpace(context, "my-hub", "my-space");
        await Assert.That(result).IsEqualTo("/admin/h/my-hub/s/my-space");
    }

    [Test]
    public async Task ManageSpace_MultiCommunity_IncludesPrefix()
    {
        var context = CreateContext(slug: "test-community", isMultiCommunity: true);
        var result = SnakkUrlHelper.ManageSpace(context, "my-hub", "my-space");
        await Assert.That(result).IsEqualTo("/admin/c/test-community/h/my-hub/s/my-space");
    }

    // ===== Community URL Tests =====

    [Test]
    public async Task Community_SingleCommunity_ReturnsRoot()
    {
        var context = CreateContext(isMultiCommunity: false);
        var result = SnakkUrlHelper.Community("main", context);
        await Assert.That(result).IsEqualTo("/");
    }

    [Test]
    public async Task Community_MultiCommunity_ReturnsCSlug()
    {
        var context = CreateContext(isMultiCommunity: true);
        var result = SnakkUrlHelper.Community("other-community", context);
        await Assert.That(result).IsEqualTo("/c/other-community");
    }

    // ===== Asset URL Tests =====

    [Test]
    public async Task Css_DistFile_ReturnsDistPath()
    {
        var result = SnakkUrlHelper.Css("site");
        await Assert.That(result).IsEqualTo("/css/dist/site.css");
    }

    [Test]
    public async Task Css_VendorFile_ReturnsVendorPath()
    {
        var result = SnakkUrlHelper.Css("tailwind", isVendor: true);
        await Assert.That(result).IsEqualTo("/css/vendor/tailwind.css");
    }

    [Test]
    public async Task Js_DistFile_ReturnsDistPath()
    {
        var result = SnakkUrlHelper.Js("app");
        await Assert.That(result).IsEqualTo("/js/dist/app.js");
    }

    [Test]
    public async Task Js_VendorFile_ReturnsVendorPath()
    {
        var result = SnakkUrlHelper.Js("htmx", isVendor: true);
        await Assert.That(result).IsEqualTo("/js/vendor/htmx.js");
    }

    [Test]
    public async Task Js_FilenameWithExtension_DoesNotDoubleExtend()
    {
        var result = SnakkUrlHelper.Js("file.js");
        await Assert.That(result).IsEqualTo("/js/dist/file.js");
    }
}
