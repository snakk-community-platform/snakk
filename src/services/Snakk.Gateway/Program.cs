var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = Environment.GetEnvironmentVariable("SNAKK_STORAGE_PATH") ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// HTTPS redirection (skip in production — Caddy/reverse proxy handles TLS)
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();

// Map reverse proxy routes
app.MapReverseProxy();

app.Run();
