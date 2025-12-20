using Microsoft.Extensions.Options;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using StackExchange.Redis;

namespace Pdmt.Api.Services
{
    public class RedisRateLimitService : IRateLimitService
    {
        private readonly IDatabase _db;
        private readonly RateLimitOptions _options;

        public RedisRateLimitService(IConnectionMultiplexer redis, IOptions<RateLimitOptions> options)
        {
            _db = redis.GetDatabase();
            _options = options.Value;
        }
        public async Task CheckAsync(string ruleName, string subject)
        {
            if (!_options.Rules.TryGetValue(ruleName, out var rule))
                return;
            var key = $"rl:{ruleName}:{subject}".ToLowerInvariant();
            var attempts = await _db.StringIncrementAsync(key);
            if (attempts == 1)
            {
                await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(rule.WindowSeconds));
            }
            if (attempts > rule.MaxAttempts)
            {
                throw new RateLimitExceededException(ruleName);
            }
        }
    }
}
