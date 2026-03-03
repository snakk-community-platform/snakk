using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Snakk.Web.TagHelpers;

/// <summary>
/// Renders a partial view inline when cached data is available,
/// otherwise emits an HTMX lazy-load div with skeleton child content.
/// </summary>
/// <example>
/// <![CDATA[
/// <inline-or-htmx partial="Shared/Sidebar/_TrendingDiscussions"
///                  model="Model.InlineTrendingDiscussions"
///                  htmx-url="/partials/trending-discussions?scopeType=hub&scopeId=abc">
///     <div class="skeleton h-8 w-full"></div>
/// </inline-or-htmx>
/// ]]>
/// </example>
[HtmlTargetElement("inline-or-htmx")]
public class InlineOrHtmxTagHelper(IHtmlHelper htmlHelper) : TagHelper
{
    /// <summary>The inline data model. If non-null, the partial is rendered immediately.</summary>
    [HtmlAttributeName("model")]
    public object? Model { get; set; }

    /// <summary>The shared partial view name (e.g. "Shared/Sidebar/_TrendingDiscussions").</summary>
    [HtmlAttributeName("partial")]
    public string Partial { get; set; } = string.Empty;

    /// <summary>The HTMX fallback URL for lazy loading when cache is cold.</summary>
    [HtmlAttributeName("htmx-url")]
    public string HtmxUrl { get; set; } = string.Empty;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (Model is not null)
        {
            // Cache is warm — render partial inline, no wrapper element
            output.TagName = null;
            (htmlHelper as IViewContextAware)?.Contextualize(ViewContext);
            var html = await htmlHelper.PartialAsync(Partial, Model);
            output.Content.SetHtmlContent(html);
        }
        else
        {
            // Cache is cold — emit HTMX lazy-load div with skeleton child content
            output.TagName = "div";
            output.Attributes.SetAttribute("hx-get", HtmxUrl);
            output.Attributes.SetAttribute("hx-trigger", "revealed");
            output.Attributes.SetAttribute("hx-target", "this");
            output.Attributes.SetAttribute("hx-swap", "innerHTML");
            output.Attributes.SetAttribute("hx-push-url", "false");
            // Child content (skeleton markup) is preserved automatically
        }
    }
}
