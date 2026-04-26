using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Testcontainers.PostgreSql;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();
}

[CollectionDefinition("ServiceTests")]
public sealed class ServiceTestsCollection : ICollectionFixture<PostgresContainerFixture>;
