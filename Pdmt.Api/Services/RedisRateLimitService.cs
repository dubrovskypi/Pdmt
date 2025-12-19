using Microsoft.Extensions.Options;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using StackExchange.Redis;

namespace Pdmt.Api.Services
{
    public class RedisRateLimitService : IRateLimitService
    {
        //private const int MaxAttempts = 3;
        //private static readonly TimeSpan Window = TimeSpan.FromMinutes(3);
        
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

        //public async Task CheckLoginAttemptAsync(string email)
        //{
        //    var key = $"auth:login:{email.ToLowerInvariant()}";
        //    var attempts = await _db.StringIncrementAsync(key);
        //    if (attempts == 1)
        //    {
        //        await _db.KeyExpireAsync(key, Window);
        //    }
        //    if (attempts > MaxAttempts)
        //    {
        //        throw new InvalidOperationException("Too many login attempts. Try again later.");
        //    }
        //}

        //public async Task CheckRefreshAttemptAsync(string token)
        //{
        //    var key = $"auth:refresh:{token}";
        //    var attempts = await _db.StringIncrementAsync(key);
        //    if (attempts == 1)
        //    {
        //        await _db.KeyExpireAsync(key, Window);
        //    }
        //    if (attempts > MaxAttempts)
        //    {
        //        throw new InvalidOperationException("Too many login attempts. Try again later.");
        //    }
        //}
    }
}
