using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pdmt.Api.Data;

/// <summary>
/// Used only by EF design-time tools (dotnet ef migrations add/remove).
/// Not used at runtime. Connection string here is for local migration generation only.
/// </summary>
public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostgresAppDbContext>()
            .UseNpgsql("Host=localhost;Database=PdmtDb_Design;Username=postgres;Password=postgres")
            .Options;

        return new PostgresAppDbContext(options);
    }
}
