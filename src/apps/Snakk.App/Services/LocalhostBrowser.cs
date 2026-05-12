namespace Snakk.App.Services;

using IdentityModel.OidcClient.Browser;
using System.Diagnostics;
using System.Net;
using System.Text;

public class LocalhostBrowser(int port) : IBrowser
{
    private static readonly string CallbackHtml =
        "<!DOCTYPE html><html><body style='font-family:system-ui;text-align:center;padding:4rem'>" +
        "<h2>You can close this window.</h2>" +
        "<p>Sign-in complete — return to the app.</p>" +
        "</body></html>";

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        var prefix = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        Process.Start(new ProcessStartInfo(options.StartUrl) { UseShellExecute = true });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            var ctx = await listener.GetContextAsync().WaitAsync(cts.Token);

            var callbackUrl = ctx.Request.Url?.ToString() ?? string.Empty;

            var responseBytes = Encoding.UTF8.GetBytes(CallbackHtml);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = responseBytes.Length;
            await ctx.Response.OutputStream.WriteAsync(responseBytes, cts.Token);
            ctx.Response.Close();

            return new BrowserResult { Response = callbackUrl, ResultType = BrowserResultType.Success };
        }
        catch (OperationCanceledException)
        {
            return new BrowserResult { ResultType = BrowserResultType.Timeout, Error = "Login timed out." };
        }
        finally
        {
            listener.Stop();
        }
    }
}
