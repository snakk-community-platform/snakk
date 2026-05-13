namespace Snakk.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

public class SnakkDbContextFactory : IDesignTimeDbContextFactory<SnakkDbContext>
{
    public SnakkDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DbConnection")
            ?? "Host=localhost;Database=snakk_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new(options);
    }
}
