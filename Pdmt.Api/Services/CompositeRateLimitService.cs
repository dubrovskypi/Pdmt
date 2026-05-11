using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Infrastructure.Metrics;
using StackExchange.Redis;

namespace Pdmt.Api.Services
{
    public class CompositeRateLimitService(
        RedisRateLimitService redis,
        InMemoryRateLimitService fallback,
        AuthMetrics metrics,
        ILogger<CompositeRateLimitService> logger) : IRateLimitService
    {
        public async Task CheckAsync(string ruleName, string subject)
        {
            try
            {
                try
                {
                    await redis.CheckAsync(ruleName, subject);
                }
                catch (RedisConnectionException ex)
                {
                    logger.LogError(ex, "Redis unavailable, falling back to in-memory rate limiting");
                    await fallback.CheckAsync(ruleName, subject);
                }
            }
            catch (RateLimitExceededException)
            {
                metrics.RateLimitTripped(ruleName);
                throw;
            }
        }
    }
}
