using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Snakk.App.Services;

public static class MauiProgram
{
    private const string ApiBaseUrl = "http://localhost:17000";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<Snakk.App.App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        builder.Services.AddSingleton(new AuthService(ApiBaseUrl));
        builder.Services.AddHttpClient<DiscussionApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiBaseUrl);
        });
        builder.Services.AddHttpClient("images", client =>
        {
            client.BaseAddress = new Uri(ApiBaseUrl);
        });
        builder.Services.AddSingleton<ImageCacheService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
