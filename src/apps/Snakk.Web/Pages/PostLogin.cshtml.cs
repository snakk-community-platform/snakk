using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages;

public class PostLoginModel : PageModel
{
    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google", "GitHub", "Discord", "Facebook", "Microsoft", "Steam", "passkey"
    };

    [BindProperty(SupportsGet = true)]
    public string? Provider { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    public string? SafeProvider { get; private set; }
    public string SafeTo { get; private set; } = "/";

    public IActionResult OnGet()
    {
        if (!string.IsNullOrEmpty(Provider) && KnownProviders.Contains(Provider))
            SafeProvider = Provider;

        var to = To ?? "/";
        SafeTo = Url.IsLocalUrl(to) ? to : "/";

        return Page();
    }
}
