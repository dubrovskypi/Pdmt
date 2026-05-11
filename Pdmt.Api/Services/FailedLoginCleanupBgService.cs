using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;

namespace Pdmt.Api.Services;

public class FailedLoginCleanupBgService(IServiceProvider sp, ILogger<FailedLoginCleanupBgService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
                var deleted = await db.FailedLoginAttempts
                    .Where(f => f.OccurredAtUtc < cutoff)
                    .ExecuteDeleteAsync(ct);
                if (deleted > 0)
                    logger.LogInformation("Cleaned {Count} expired failed-login attempts", deleted);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed-login cleanup iteration failed");
            }

            try { await Task.Delay(TimeSpan.FromHours(6), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
