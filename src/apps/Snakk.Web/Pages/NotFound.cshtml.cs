using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages;

public class NotFoundModel : PageModel
{
    public void OnGet()
    {
        Response.StatusCode = 404;
    }
}
