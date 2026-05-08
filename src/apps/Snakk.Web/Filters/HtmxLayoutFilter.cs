using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Filters;

public class HtmxLayoutFilter : IPageFilter
{
    public void OnPageHandlerSelected(PageHandlerSelectedContext context)
    {
    }

    public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        // Check if this is an HTMX boosted request
        if (context.HttpContext.Request.Headers.ContainsKey("HX-Request")
            && (context.HttpContext.Request.Headers.ContainsKey("HX-Boosted")
                || context.HttpContext.Request.Headers.ContainsKey("HX-History-Restore-Request")))
        {
            // Set a flag that _ViewStart can use to switch layouts
            context.HttpContext.Items["UsePartialLayout"] = true;
        }
    }

    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
    }
}
