using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Tests for SnakkUrlHelper static methods which generate community-aware URLs.
/// Uses real CommunityContext instances to test URL prefix logic.
/// </summary>
public class SnakkUrlHelperTests
{
    private ICommunityContext CreateContext(
        string slug = "test-community",
        bool isDefault = true,
        bool isCustomDomain = false,
        bool isMultiCommunity = false)
    {
        var context = new CommunityContext();
        context.SetCommunity(slug, isDefault, isCustomDomain, "Test Community", isMultiCommunity);

        return context;
    }

    // ===== Community Prefix Tests =====

    [Test]
    public async Task CommunityPrefix_DefaultCommunity_ReturnsEmpty()
    {
        var context = CreateContext(isDefault: true, isMultiCommunity: false);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CommunityPrefix_CustomDomain_ReturnsEmpty()
    {
        var context = CreateContext(isDefault: false, isCustomDomain: true);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CommunityPrefix_MultiCommunity_ReturnsCSlug()
    {
        var context = CreateContext(slug: "my-community", isDefault: true, isMultiCommunity: true);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("/c/my-community");
    }

    [Test]
    public async Task CommunityPrefix_NonDefault_ReturnsCSlug()
    {
        var context = CreateContext(slug: "other-community", isDefault: false);
        var result = SnakkUrlHelper.CommunityPrefix(context);
        await Assert.That(result).IsEqualTo("/c/other-community");
    }

    // ===== Hub URL Tests =====

    [Test]
    public async Task Hub_DefaultCommunity_OmitsPrefix()
    {
        var context = CreateContext(isDefault: true);
        var result = SnakkUrlHelper.Hub(context, "tech");
        await Assert.That(result).IsEqualTo("/h/tech");
    }

    [Test]
    public async Task Hub_NonDefault_IncludesPrefix()
    {
        var context = CreateContext(slug: "gaming", isDefault: false);
        var result = SnakkUrlHelper.Hub(context, "fps");
        await Assert.That(result).IsEqualTo("/c/gaming/h/fps");
    }

    [Test]
    public async Task Hub_ExplicitSlug_AlwaysIncludesPrefix()
    {
        var result = SnakkUrlHelper.Hub("any-community", "my-hub");
        await Assert.That(result).IsEqualTo("/c/any-community/h/my-hub");
    }

    // ===== Space URL Tests =====

    [Test]
    public async Task Space_DefaultCommunity_OmitsPrefix()
    {
        var context = CreateContext(isDefault: true);
        var result = SnakkUrlHelper.Space(context, "tech", "csharp");
        await Assert.That(result).IsEqualTo("/h/tech/csharp");
    }

    [Test]
    public async Task Space_NonDefault_IncludesPrefix()
    {
        var context = CreateContext(slug: "gaming", isDefault: false);
        var result = SnakkUrlHelper.Space(context, "fps", "valorant");
        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/valorant");
    }

    [Test]
    public async Task Space_ExplicitSlug_AlwaysIncludesPrefix()
    {
        var result = SnakkUrlHelper.Space("any-community", "my-hub", "my-space");
        await Assert.That(result).IsEqualTo("/c/any-community/h/my-hub/my-space");
    }

    // ===== Discussion URL Tests =====

    [Test]
    public async Task Discussion_DefaultCommunity_OmitsPrefix()
    {
        var context = CreateContext(isDefault: true);
        var result = SnakkUrlHelper.Discussion(context, "tech", "csharp", "my-thread-abc123");
        await Assert.That(result).IsEqualTo("/h/tech/csharp/my-thread-abc123");
    }

    [Test]
    public async Task Discussion_NonDefault_IncludesPrefix()
    {
        var context = CreateContext(slug: "gaming", isDefault: false);
        var result = SnakkUrlHelper.Discussion(context, "fps", "valorant", "patch-notes-xyz789");
        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/valorant/patch-notes-xyz789");
    }

    // ===== Manage URL Tests =====

    [Test]
    public async Task ManageCommunity_AlwaysIncludesSlug()
    {
        var result = SnakkUrlHelper.ManageCommunity("test-community");
        await Assert.That(result).IsEqualTo("/c/test-community/manage");
    }

    [Test]
    public async Task ManageHub_AlwaysIncludesSlug()
    {
        var result = SnakkUrlHelper.ManageHub("test-community", "my-hub");
        await Assert.That(result).IsEqualTo("/c/test-community/h/my-hub/manage");
    }

    [Test]
    public async Task ManageSpace_AlwaysIncludesSlug()
    {
        var result = SnakkUrlHelper.ManageSpace("test-community", "my-hub", "my-space");
        await Assert.That(result).IsEqualTo("/c/test-community/h/my-hub/s/my-space/manage");
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
