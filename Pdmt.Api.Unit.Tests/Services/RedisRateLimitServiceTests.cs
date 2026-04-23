using Microsoft.Extensions.Options;
using Moq;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;
using StackExchange.Redis;

namespace Pdmt.Api.Unit.Tests.Services
{
    public class RedisRateLimitServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _multiplexer = new();
        private readonly Mock<IDatabase> _db = new();

        public RedisRateLimitServiceTests()
        {
            _multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                        .Returns(_db.Object);
        }

        private RedisRateLimitService CreateService(int maxAttempts, int windowSeconds)
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
            return new RedisRateLimitService(_multiplexer.Object, options);
        }

        [Fact]
        public async Task CheckAsync_UnknownRule_DoesNotCallRedis()
        {
            var service = CreateService(5, 60);

            await service.CheckAsync("UnknownRule", "192.168.1.1");

            _db.Verify(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Fact]
        public async Task CheckAsync_FirstRequest_SetsExpiry()
        {
            _db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(1);
            _db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

            var service = CreateService(5, 60);

            await service.CheckAsync("TestRule", "192.168.1.1");

            _db.Verify(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                TimeSpan.FromSeconds(60),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task CheckAsync_SubsequentRequest_DoesNotSetExpiry()
        {
            _db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(2);

            var service = CreateService(5, 60);

            await service.CheckAsync("TestRule", "192.168.1.1");

            _db.Verify(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()), Times.Never);
        }

        [Fact]
        public async Task CheckAsync_AtExactLimit_DoesNotThrow()
        {
            _db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(5);

            var service = CreateService(5, 60);

            await service.CheckAsync("TestRule", "192.168.1.1");
        }

        [Fact]
        public async Task CheckAsync_ExceedsLimit_ThrowsRateLimitExceededException()
        {
            _db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(6);

            var service = CreateService(5, 60);

            await Assert.ThrowsAsync<RateLimitExceededException>(() =>
                service.CheckAsync("TestRule", "192.168.1.1"));
        }

        [Fact]
        public async Task CheckAsync_KeyIsLowercased()
        {
            _db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(1);
            _db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

            var service = CreateService(5, 60);

            await service.CheckAsync("TestRule", "IP-UPPERCASE");

            _db.Verify(d => d.StringIncrementAsync(
                It.Is<RedisKey>(k => ((string)k!).Contains("ip-uppercase")),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }
    }
}
