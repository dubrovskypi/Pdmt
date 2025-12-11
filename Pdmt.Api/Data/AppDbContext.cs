using Microsoft.EntityFrameworkCore;

namespace Pdmt.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Add any additional model configurations here
    }
    // Define DbSets for your entities here
    // public DbSet<YourEntity> YourEntities { get; set; }
}
