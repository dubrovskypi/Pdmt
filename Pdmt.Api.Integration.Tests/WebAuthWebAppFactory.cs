using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests;

public class WebAuthWebAppFactory : CustomWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRateLimitService>();
            services.AddScoped<IRateLimitService>(_ => new NoOpRateLimitService());
        });
    }

    private sealed class NoOpRateLimitService : IRateLimitService
    {
        public Task CheckAsync(string ruleName, string subject) => Task.CompletedTask;
    }
}
