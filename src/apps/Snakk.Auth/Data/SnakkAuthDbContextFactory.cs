namespace Snakk.Auth.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

public class SnakkAuthDbContextFactory : IDesignTimeDbContextFactory<SnakkAuthDbContext>
{
    public SnakkAuthDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var options = new DbContextOptionsBuilder<SnakkAuthDbContext>()
            .UseNpgsql(configuration.GetConnectionString("AuthDbConnection"))
            .Options;

        return new(options);
    }
}
