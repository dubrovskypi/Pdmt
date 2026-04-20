using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests
{
    public class InMemoryRateLimitServiceTests
    {
        private InMemoryRateLimitService CreateService(int maxAttempts, int windowSeconds)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new RateLimitOptions
            {
                Rules = new Dictionary<string, RateLimitRule>
                {
                    ["TestRule"] = new RateLimitRule
                    {
                        MaxAttempts = maxAttempts,
                        WindowSeconds = windowSeconds
                    }
                }
            });
            return new InMemoryRateLimitService(cache, options);
        }

        [Fact]
        public async Task CheckAsync_BelowLimit_DoesNotThrow()
        {
            var service = CreateService(5, 60);

            for (int i = 0; i < 3; i++)
            {
                await service.CheckAsync("TestRule", "192.168.1.1");
            }
            // No exception thrown
        }

        [Fact]
        public async Task CheckAsync_AtExactLimit_DoesNotThrow()
        {
            var service = CreateService(5, 60);

            for (int i = 0; i < 5; i++)
            {
                await service.CheckAsync("TestRule", "192.168.1.1");
            }
            // No exception thrown
        }

        [Fact]
        public async Task CheckAsync_ExceedsLimit_ThrowsRateLimitExceededException()
        {
            var service = CreateService(5, 60);

            for (int i = 0; i < 5; i++)
            {
                await service.CheckAsync("TestRule", "192.168.1.1");
            }

            await Assert.ThrowsAsync<RateLimitExceededException>(() =>
                service.CheckAsync("TestRule", "192.168.1.1"));
        }

        [Fact]
        public async Task CheckAsync_ExceededException_ContainsRuleName()
        {
            var service = CreateService(2, 60);

            await service.CheckAsync("TestRule", "192.168.1.1");
            await service.CheckAsync("TestRule", "192.168.1.1");

            var ex = await Assert.ThrowsAsync<RateLimitExceededException>(() =>
                service.CheckAsync("TestRule", "192.168.1.1"));

            Assert.Equal("TestRule", ex.Rule);
        }

        [Fact]
        public async Task CheckAsync_DifferentSubjects_CountedSeparately()
        {
            var service = CreateService(3, 60);

            // Use up limit for 192.168.1.1
            for (int i = 0; i < 3; i++)
            {
                await service.CheckAsync("TestRule", "192.168.1.1");
            }

            // Different subject should not be affected
            await service.CheckAsync("TestRule", "192.168.1.2");
            // No exception thrown
        }

        [Fact]
        public async Task CheckAsync_UnknownRuleName_DoesNotThrow()
        {
            var service = CreateService(5, 60);

            await service.CheckAsync("UnknownRule", "192.168.1.1");
            // No exception thrown
        }

        [Fact]
        public async Task CheckAsync_DifferentRuleNames_CountedSeparately()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new RateLimitOptions
            {
                Rules = new Dictionary<string, RateLimitRule>
                {
                    ["Rule1"] = new RateLimitRule { MaxAttempts = 2, WindowSeconds = 60 },
                    ["Rule2"] = new RateLimitRule { MaxAttempts = 2, WindowSeconds = 60 }
                }
            });
            var service = new InMemoryRateLimitService(cache, options);

            // Use up limit for Rule1
            await service.CheckAsync("Rule1", "192.168.1.1");
            await service.CheckAsync("Rule1", "192.168.1.1");

            // Rule2 should not be affected
            await service.CheckAsync("Rule2", "192.168.1.1");
            await service.CheckAsync("Rule2", "192.168.1.1");
            // No exception thrown
        }
    }
}
