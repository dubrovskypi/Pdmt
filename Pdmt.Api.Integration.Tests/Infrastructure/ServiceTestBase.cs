using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

[Collection("ServiceTests")]
public abstract class ServiceTestBase(PostgresContainerFixture fixture) : IAsyncLifetime
{
    protected AppDbContext Db { get; private set; } = null!;

    public readonly Guid TestUserId = TestUserHelper.TestUserId;

    public virtual async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        Db = new AppDbContext(options);
        await TestDatabaseCleaner.CleanAsync(Db);
        Db.Users.Add(new UserBuilder().WithId(TestUserId).WithEmail("test@pdmt.dev").Build());
        await Db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}
