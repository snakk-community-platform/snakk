using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Snakk.Web.Pages.Partials;

[OutputCache(PolicyName = "AnonymousPartial")]
public class LeftSidebarModel : PageModel
{
    public void OnGet() { }
}
