using Microsoft.AspNetCore.Authentication.Cookies;
using Snakk.AdminWeb.Middleware;
using Snakk.AdminWeb.Services;
using Snakk.Sdk;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();

// HTTP Client for Snakk API with auth header handler
builder.Services.AddTransient<AdminAuthHeaderHandler>();
builder.Services.AddHttpClient<SnakkApiClient>(client =>
{
    var baseUrl = builder.Configuration["SnakkApi:BaseUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
})
.AddHttpMessageHandler<AdminAuthHeaderHandler>();

builder.Services.AddScoped<AdminApiClientService>();

// Cookie-based authentication for admin
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "Snakk.AdminAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Session for temp data
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware
app.UseMiddleware<AdminAuthenticationMiddleware>();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
