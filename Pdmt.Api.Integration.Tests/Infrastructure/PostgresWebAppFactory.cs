using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Net.Http.Headers;
using Moq;
using Pdmt.Api.Data;
using Pdmt.Api.Services;
using StackExchange.Redis;
using Testcontainers.PostgreSql;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public sealed class PostgresWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "test-super-secret-key-min-32-chars!!";
    public const string TestJwtIssuer = "pdmt-test";
    public const string TestJwtAudience = "pdmt-test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public new async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:TokenLifetimeMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenLifetimeDays", "1");
        builder.UseSetting("App:DefaultTimeZone", "Europe/Vilnius");
        // Program.cs does fail-fast validation for these before ConfigureServices runs
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379,abortConnect=false");
        builder.UseSetting("OpenTelemetry:Endpoint", "http://localhost:4317");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://localhost:5173");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(new Mock<IConnectionMultiplexer>().Object);

            services.RemoveAll<IRateLimitService>();
            services.AddSingleton<IRateLimitService, NoOpRateLimitService>();

            // Add TestScheme + TestOrJwt PolicyScheme
            services.AddAuthentication()
                .AddPolicyScheme("TestOrJwt", null, o =>
                {
                    o.ForwardDefaultSelector = ctx =>
                    {
                        var header = ctx.Request.Headers[HeaderNames.Authorization]
                            .FirstOrDefault();
                        return header?.StartsWith(TestAuthHandler.SchemeName) == true
                            ? TestAuthHandler.SchemeName
                            : JwtBearerDefaults.AuthenticationScheme;
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Override default scheme to "TestOrJwt" (runs after all Configure calls)
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = "TestOrJwt";
                o.DefaultAuthenticateScheme = "TestOrJwt";
                o.DefaultChallengeScheme = "TestOrJwt";
            });
        });
    }
}

internal sealed class NoOpRateLimitService : IRateLimitService
{
    public Task CheckAsync(string ruleName, string subject) => Task.CompletedTask;
}
