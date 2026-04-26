using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;
using System.Net.Http.Headers;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public abstract class HttpTestBase : IClassFixture<PostgresWebAppFactory>, IAsyncLifetime
{
    protected readonly PostgresWebAppFactory Factory;
    protected HttpClient Client;
    protected readonly Guid TestUserId = TestUserHelper.TestUserId;

    protected HttpTestBase(PostgresWebAppFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
    }

    public virtual async ValueTask InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await TestDatabaseCleaner.CleanAsync(db);
        db.Users.Add(new UserBuilder().WithId(TestUserId).WithEmail("test@pdmt.dev").Build());
        await db.SaveChangesAsync();
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
