using Snakk.Application.Services;
using Snakk.Infrastructure.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the new admin/manage data services extracted from Snakk.Api endpoints
/// and gRPC services. Call this from Snakk.Api's Program.cs (or via the existing
/// ServiceCollectionExtensions) — see the integrator notes in the refactor report.
/// </summary>
public static class AdminDataServiceRegistration
{
    public static IServiceCollection AddAdminDataServices(this IServiceCollection services)
    {
        // Admin moderation data: UnbanUser active-ban lookup, UpdateUserRole info,
        // GetReport detail projection (AdminModerationEndpoints)
        services.AddScoped<IAdminModerationDataService, AdminModerationDataService>();

        // Manage scope resolution + gRPC manage service data queries:
        // scope hierarchy, Discord settings, global-admin check, user lookups, moderator lists
        services.AddScoped<IManageScopeDataService, ManageScopeDataService>();

        // NOTE: IAdminContentService (AdminContentService) and IAdminUserService (AdminUserService)
        // are already registered in ServiceCollectionExtensions.cs lines 505-506.
        // Do NOT register them again here.

        return services;
    }
}
