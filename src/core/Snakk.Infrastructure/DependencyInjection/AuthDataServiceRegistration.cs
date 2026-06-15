namespace Microsoft.Extensions.DependencyInjection;

using Snakk.Application.Services;
using Snakk.Infrastructure.Services;

/// <summary>
/// Registers the auth-area data services that were extracted from
/// Snakk.Api during the Clean Architecture refactor (SnakkDbContext removal).
///
/// Call this from the host's service registration (e.g. Program.cs or
/// AddSnakkServices) — the two entries previously registered in Program.cs
/// under Snakk.Api.Services.* are now stale and must be removed/replaced.
/// See "Stale registrations" in the refactor report.
/// </summary>
public static class AuthDataServiceRegistration
{
    public static IServiceCollection AddAuthDataServices(this IServiceCollection services)
    {
        // Auth-area data services (encapsulate SnakkDbContext usage from Api layer)
        services.AddScoped<IAuthDataService, AuthDataService>();
        services.AddScoped<ITwoFactorDataService, TwoFactorDataService>();
        services.AddScoped<IMeDataService, MeDataService>();

        // AuthVersionCache + Sweeper — moved from Snakk.Api.Services to here.
        // IAuthVersionCache was previously registered as Singleton pointing to
        // Snakk.Api.Services.AuthVersionCache; that registration in Program.cs
        // must be replaced with the line below.
        services.AddSingleton<IAuthVersionCache, AuthVersionCache>();
        services.AddSingleton<IUserVisitTracker, UserVisitTracker>();

        // AuthVersionSweeper was previously registered as a HostedService in
        // Program.cs pointing to Snakk.Api.Services.AuthVersionSweeper.
        // That AddHostedService call in Program.cs must be updated to point here.
        services.AddHostedService<AuthVersionSweeper>();

        return services;
    }
}
