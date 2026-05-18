using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class LegalModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public string Title { get; set; } = "";
    public new string Content { get; set; } = "";
    public int VersionNumber { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(slug))
            return NotFound();

        var result = await apiClient.GetConsentTextAsync(slug);
        if (result is null)
            return NotFound();

        // Map slug to display title
        Title = slug switch
        {
            "terms-of-service" => "Terms of Service",
            "privacy-policy" => "Privacy Policy",
            _ => slug.Replace("-", " ")
        };
        Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Title);

        Content = result.Text;
        VersionNumber = result.VersionNumber;

        return Page();
    }
}
