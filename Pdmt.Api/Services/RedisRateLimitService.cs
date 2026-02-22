using Microsoft.Extensions.Options;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using StackExchange.Redis;

namespace Pdmt.Api.Services
{
    public class RedisRateLimitService : IRateLimitService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly RateLimitOptions _options;

        public RedisRateLimitService(IConnectionMultiplexer redis, IOptions<RateLimitOptions> options)
        {
            _redis = redis;
            _options = options.Value;
        }
        public async Task CheckAsync(string ruleName, string subject)
        {
            if (!_options.Rules.TryGetValue(ruleName, out var rule))
                return;
            var db = _redis.GetDatabase();
            var key = $"rl:{ruleName}:{subject}".ToLowerInvariant();
            var attempts = await db.StringIncrementAsync(key);
            if (attempts == 1)
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(rule.WindowSeconds));
            if (attempts > rule.MaxAttempts)
                throw new RateLimitExceededException(ruleName);
        }
    }
}
