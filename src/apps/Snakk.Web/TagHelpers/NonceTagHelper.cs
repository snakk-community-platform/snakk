using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Snakk.Web.TagHelpers;

[HtmlTargetElement("script")]
[HtmlTargetElement("link", Attributes = "as")]
public class NonceTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.TagName == "link" &&
            output.Attributes["as"]?.Value?.ToString() != "script")
            return;

        if (ViewContext.HttpContext.Items.TryGetValue("csp-nonce", out var nonce) && nonce is string nonceStr)
            output.Attributes.SetAttribute("nonce", nonceStr);
    }
}
