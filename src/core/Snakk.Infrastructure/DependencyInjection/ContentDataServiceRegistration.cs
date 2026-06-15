namespace Microsoft.Extensions.DependencyInjection;

using Snakk.Application.Services;
using Snakk.Infrastructure.Services;

public static class ContentDataServiceRegistration
{
    /// <summary>
    /// Registers the Application-layer data-service interfaces introduced to remove
    /// direct SnakkDbContext / Snakk.Infrastructure.Database references from Snakk.Api.
    /// Call this from the Api's ServiceCollectionExtensions or Program.cs.
    /// </summary>
    public static IServiceCollection AddContentDataServices(this IServiceCollection services)
    {
        services.AddScoped<IPostAccessDataService, PostAccessDataService>();
        services.AddScoped<ICommunityDataService, CommunityDataService>();
        services.AddScoped<IHubDataService, HubDataService>();
        services.AddScoped<ISpaceDataService, SpaceDataService>();
        services.AddScoped<IUserDataService, UserDataService>();
        services.AddScoped<ISlugService, SlugService>();

        return services;
    }
}
