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
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseNpgsql(
                configuration.GetConnectionString("DbConnection"))
            .Options;

        return new(options);
    }
}
