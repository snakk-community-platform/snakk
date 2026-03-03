using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Snakk.ServiceDefaults;

public static class Extensions
{
    /// <summary>
    /// Adds Serilog structured logging with console sink, environment enrichers,
    /// and configuration-driven overrides via the "Serilog" config section.
    /// </summary>
    public static IHostApplicationBuilder AddSnakkDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog(config =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Routing", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Grpc", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName);

            if (builder.Environment.IsDevelopment())
            {
                // Human-readable output for local development
                config.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}");
            }
            else
            {
                // Structured JSON for production (easily ingested by log aggregators)
                config.WriteTo.Console(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter());
            }
        });

        return builder;
    }
}
