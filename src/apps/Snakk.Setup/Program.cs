using Snakk.Setup.Services;

var builder = WebApplication.CreateBuilder(args);

// Load shared config (written by the setup wizard itself)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"]
    ?? Environment.GetEnvironmentVariable("SNAKK_STORAGE_PATH")
    ?? "/app/storage";
var confDir = Path.Combine(sharedConfigDir, "conf");
builder.Configuration.AddJsonFile(
    Path.Combine(confDir, "snakk-config.json"),
    optional: true, reloadOnChange: true);

// Session (wizard state across pages)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddRazorPages();
builder.Services.AddScoped<SetupService>();

var app = builder.Build();

// Block all access if setup is already complete
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (File.Exists(Path.Combine(confDir, "snakk-config.json"))
        && !path.StartsWith("/setup/install", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/setup/restarting", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/setup/css", StringComparison.OrdinalIgnoreCase)
        && !path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Setup has already been completed. Access denied.");
        return;
    }
    await next();
});

// Redirect root to /setup (pages now live under /setup/*)
app.MapGet("/", () => Results.Redirect("/setup"));

app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/setup"
});
app.UseSession();

// Require setup password — must be set in any non-development environment
var setupPassword = Environment.GetEnvironmentVariable("SETUP_PASSWORD");
if (string.IsNullOrEmpty(setupPassword) && !app.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "SETUP_PASSWORD environment variable must be set. " +
        "Generate one with: openssl rand -base64 18 | tr -d '/+=' | head -c 16");

if (!string.IsNullOrEmpty(setupPassword))
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/setup/css", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var isAuthenticated = context.Session.GetString("SetupAuthenticated") == "true";
        if (!isAuthenticated && !path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/setup");
            return;
        }

        await next();
    });
}
app.UseRouting();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
