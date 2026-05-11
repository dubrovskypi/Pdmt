using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;

namespace Pdmt.Api.Services;

public class TokenCleanupBgService(
    IServiceProvider sp,
    ILogger<TokenCleanupBgService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var deleted = await db.RefreshTokens
                    .Where(t => t.ExpiresAt < DateTimeOffset.UtcNow)
                    .ExecuteDeleteAsync(ct);
                if (deleted > 0)
                    logger.LogInformation("Cleaned {Count} expired refresh tokens", deleted);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh-token cleanup iteration failed");
            }

            try { await Task.Delay(TimeSpan.FromHours(6), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
