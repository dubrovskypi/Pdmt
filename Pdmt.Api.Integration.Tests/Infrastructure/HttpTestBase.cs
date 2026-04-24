using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using System.Net.Http.Headers;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public abstract class HttpTestBase : IClassFixture<PostgresWebAppFactory>, IAsyncLifetime
{
    protected readonly PostgresWebAppFactory Factory;
    protected HttpClient Client;
    protected static readonly Guid TestUserId = TestAuthHandler.TestUserId;

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
        await SeedDefaultUserAsync(db);
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static async Task SeedDefaultUserAsync(AppDbContext db)
    {
        db.Users.Add(new User
        {
            Id = TestAuthHandler.TestUserId,
            Email = "test@pdmt.dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
