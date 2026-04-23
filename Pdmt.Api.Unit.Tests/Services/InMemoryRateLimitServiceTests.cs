using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests
{
    public class InMemoryRateLimitServiceTests
    {
        private static InMemoryRateLimitService CreateService(int maxAttempts, int windowSeconds, TimeProvider? timeProvider = null)
        {
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
            return new InMemoryRateLimitService(options, timeProvider);
        }

        [Fact]
        public async Task CheckAsync_BelowLimit_DoesNotThrow()
        {
            var service = CreateService(5, 60);

            for (int i = 0; i < 3; i++)
                await service.CheckAsync("TestRule", "192.168.1.1");
        }

        [Fact]
        public async Task CheckAsync_AtExactLimit_DoesNotThrow()
        {
            var service = CreateService(5, 60);

            for (int i = 0; i < 5; i++)
                await service.CheckAsync("TestRule", "192.168.1.1");
        }

        [Fact]
        public async Task CheckAsync_ExceedsLimit_ThrowsRateLimitExceededException()
        {
            var service = CreateService(5, 60);

            for (int i = 0; i < 5; i++)
                await service.CheckAsync("TestRule", "192.168.1.1");

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

            for (int i = 0; i < 3; i++)
                await service.CheckAsync("TestRule", "192.168.1.1");

            await service.CheckAsync("TestRule", "192.168.1.2");
        }

        [Fact]
        public async Task CheckAsync_UnknownRuleName_DoesNotThrow()
        {
            var service = CreateService(5, 60);

            await service.CheckAsync("UnknownRule", "192.168.1.1");
        }

        [Fact]
        public async Task CheckAsync_DifferentRuleNames_CountedSeparately()
        {
            var options = Options.Create(new RateLimitOptions
            {
                Rules = new Dictionary<string, RateLimitRule>
                {
                    ["Rule1"] = new RateLimitRule { MaxAttempts = 2, WindowSeconds = 60 },
                    ["Rule2"] = new RateLimitRule { MaxAttempts = 2, WindowSeconds = 60 }
                }
            });
            var service = new InMemoryRateLimitService(options);

            await service.CheckAsync("Rule1", "192.168.1.1");
            await service.CheckAsync("Rule1", "192.168.1.1");

            await service.CheckAsync("Rule2", "192.168.1.1");
            await service.CheckAsync("Rule2", "192.168.1.1");
        }

        [Fact]
        public async Task CheckAsync_WindowExpired_ResetsCounter()
        {
            var fake = new FakeTimeProvider();
            var service = CreateService(maxAttempts: 2, windowSeconds: 60, timeProvider: fake);

            await service.CheckAsync("TestRule", "192.168.1.1");
            await service.CheckAsync("TestRule", "192.168.1.1");
            await Assert.ThrowsAsync<RateLimitExceededException>(() =>
                service.CheckAsync("TestRule", "192.168.1.1"));

            fake.Advance(TimeSpan.FromSeconds(61));

            await service.CheckAsync("TestRule", "192.168.1.1");
        }

        [Fact]
        public async Task CheckAsync_ParallelRequests_AtomicCounterUnderSemaphore()
        {
            var service = CreateService(maxAttempts: 5, windowSeconds: 60);
            const int total = 8;

            var tasks = Enumerable.Range(0, total)
                .Select(_ => Task.Run(() => service.CheckAsync("TestRule", "192.168.1.1")));

            var results = await Task.WhenAll(tasks.Select(t => t.ContinueWith(x => x.Exception)));

            int thrown = results.Count(e => e?.InnerException is RateLimitExceededException);
            int passed = results.Count(e => e is null);

            Assert.Equal(5, passed);
            Assert.Equal(3, thrown);
        }
    }
}
