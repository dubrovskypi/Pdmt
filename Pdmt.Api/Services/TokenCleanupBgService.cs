using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;

namespace Pdmt.Api.Services;

public class TokenCleanupBgService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var expired = await db.RefreshTokens
                .Where(t => t.ExpiresAt < DateTimeOffset.UtcNow)
                .ExecuteDeleteAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
