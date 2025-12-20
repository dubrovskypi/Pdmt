using StackExchange.Redis;

namespace Pdmt.Api.Services
{
    public class CompositeRateLimitService : IRateLimitService
    {
        private readonly RedisRateLimitService _redis;
        private readonly InMemoryRateLimitService _fallback;
        private readonly ILogger<CompositeRateLimitService> _logger;

        public CompositeRateLimitService(RedisRateLimitService redis,
                                         InMemoryRateLimitService fallback,
                                         ILogger<CompositeRateLimitService> logger)
        {
            _redis = redis;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task CheckAsync(string ruleName, string subject)
        {
            try
            {
                await _redis.CheckAsync(ruleName, subject);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis unavailable, falling back to in-memory rate limiting");
                await _fallback.CheckAsync(ruleName, subject);
            }
        }
    }
}
