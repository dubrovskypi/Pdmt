using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Pdmt.Api.Data;
using Pdmt.Api.Services;
using StackExchange.Redis;
using System.Text;
using Testcontainers.PostgreSql;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public sealed class PostgresWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSecret = "test-super-secret-key-min-32-chars!!";

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
        builder.UseSetting("Jwt:Issuer", "pdmt-test");
        builder.UseSetting("Jwt:Audience", "pdmt-test");
        builder.UseSetting("Jwt:TokenLifetimeMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenLifetimeDays", "1");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://localhost:5173");
        builder.UseSetting("App:DefaultTimeZone", "Europe/Vilnius");

        builder.ConfigureServices(services =>
        {
            RemoveService<DbContextOptions<AppDbContext>>(services);
            RemoveService<AppDbContext>(services);
            services.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql(_postgres.GetConnectionString()));

            RemoveService<IConnectionMultiplexer>(services);
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false"));

            services.RemoveAll<IRateLimitService>();
            services.AddSingleton<IRateLimitService, NoOpRateLimitService>();

            services.AddAuthentication(o =>
            {
                o.DefaultScheme = "TestOrJwt";
                o.DefaultAuthenticateScheme = "TestOrJwt";
                o.DefaultChallengeScheme = "TestOrJwt";
            })
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
                TestAuthHandler.SchemeName, _ => { })
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(TestJwtSecret))
                };
            });
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null) services.Remove(descriptor);
    }
}

internal sealed class NoOpRateLimitService : IRateLimitService
{
    public Task CheckAsync(string ruleName, string subject) => Task.CompletedTask;
}
