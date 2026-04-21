using Microsoft.Extensions.Options;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using System.Collections.Concurrent;

namespace Pdmt.Api.Services
{
    public class InMemoryRateLimitService : IRateLimitService
    {
        private readonly RateLimitOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset ExpiresAt)> _counters = new();

        public InMemoryRateLimitService(IOptions<RateLimitOptions> options, TimeProvider? timeProvider = null)
        {
            _options = options.Value;
            _timeProvider = timeProvider ?? TimeProvider.System;
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
                var now = _timeProvider.GetUtcNow();
                var (count, expiresAt) = _counters.GetValueOrDefault(key, (0, now));

                if (now >= expiresAt)
                {
                    expiresAt = now.AddSeconds(rule.WindowSeconds);
                    count = 0;
                }

                count++;
                _counters[key] = (count, expiresAt);

                if (count > rule.MaxAttempts)
                    throw new RateLimitExceededException(ruleName);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}
