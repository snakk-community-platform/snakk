using Snakk.Setup.Services;

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by the setup wizard itself)
var sharedConfigDir = Environment.GetEnvironmentVariable("SNAKK_STORAGE_PATH") ?? "/app/storage";
builder.Configuration.AddJsonFile(
    Path.Combine(sharedConfigDir, "appsettings.Production.json"),
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

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
