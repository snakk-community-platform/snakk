namespace Snakk.Auth.Data;

using Microsoft.EntityFrameworkCore;

public class SnakkAuthDbContext(DbContextOptions<SnakkAuthDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();
    }
}
