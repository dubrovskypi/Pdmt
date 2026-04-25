using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;
using Testcontainers.PostgreSql;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public abstract class ServiceTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    protected AppDbContext Db { get; private set; } = null!;

    public readonly Guid TestUserId = TestUserHelper.TestUserId;

    public virtual async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        Db = new AppDbContext(options);
        await Db.Database.MigrateAsync();
        await TestDatabaseCleaner.CleanAsync(Db);
        Db.Users.Add(new UserBuilder().WithId(TestUserId).WithEmail("test@pdmt.dev").Build());
        await Db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
