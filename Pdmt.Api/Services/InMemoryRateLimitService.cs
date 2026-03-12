
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using System.Collections.Concurrent;

namespace Pdmt.Api.Services
{
    public class InMemoryRateLimitService : IRateLimitService
    {
        private readonly IMemoryCache _cache;
        private readonly RateLimitOptions _options;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public InMemoryRateLimitService(IMemoryCache cache, IOptions<RateLimitOptions> options)
        {
            _cache = cache;
            _options = options.Value;
        }

        public async Task CheckAsync(string ruleName, string subject)
        {
            if (!_options.Rules.TryGetValue(ruleName, out var rule))
                return;

            var key = $"rl:{ruleName}:{subject}";
            var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                var counter = _cache.GetOrCreate(key, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(rule.WindowSeconds);
                    return 0;
                });
                counter++;
                _cache.Set(key, counter);
                if (counter > rule.MaxAttempts)
                    throw new RateLimitExceededException(ruleName);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}
