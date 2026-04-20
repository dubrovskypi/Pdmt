using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Integration.Tests
{
    public class AuthServiceTests
    {
        private AppDbContext CreateDbContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private IConfiguration CreateConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "test-secret-key-long-enough-32chars!",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                    ["Jwt:TokenLifetimeMinutes"] = "60",
                    ["Jwt:RefreshTokenLifetimeDays"] = "7"
                })
                .Build();

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        private Mock<IRateLimitService> CreateRateLimitMock()
        {
            var mock = new Mock<IRateLimitService>();
            mock.Setup(x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        #region RegisterAsync

        [Fact]
        public async Task RegisterAsync_ValidCredentials_ReturnsAuthResult()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            var result = await service.RegisterAsync(dto, "192.168.1.1");

            Assert.NotNull(result.AccessToken);
            Assert.NotEmpty(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
            Assert.NotEmpty(result.RefreshToken);
        }

        [Fact]
        public async Task RegisterAsync_ValidCredentials_CreatesUserInDb()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            await service.RegisterAsync(dto, "192.168.1.1");

            Assert.Single(db.Users);
            var user = await db.Users.FirstAsync();
            Assert.Equal("test@example.com", user.Email);
        }

        [Fact]
        public async Task RegisterAsync_ValidCredentials_HashesPassword()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var password = "password123";
            var dto = new UserDto { Email = "test@example.com", Password = password };
            await service.RegisterAsync(dto, "192.168.1.1");

            var user = await db.Users.FirstAsync();
            Assert.NotEqual(password, user.PasswordHash);
        }

        [Fact]
        public async Task RegisterAsync_ValidCredentials_CreatesRefreshToken()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            await service.RegisterAsync(dto, "192.168.1.1");

            Assert.Single(db.RefreshTokens);
        }

        [Fact]
        public async Task RegisterAsync_EmailWithUppercase_NormalizesEmail()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var dto = new UserDto { Email = "Test@EXAMPLE.COM", Password = "password123" };
            await service.RegisterAsync(dto, "192.168.1.1");

            var user = await db.Users.FirstAsync();
            Assert.Equal("test@example.com", user.Email);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            await service.RegisterAsync(dto, "192.168.1.1");

            var dto2 = new UserDto { Email = "test@example.com", Password = "password456" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(dto2, "192.168.1.2"));
        }

        #endregion

        #region LoginAsync

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var password = "password123";
            var registerDto = new UserDto { Email = "test@example.com", Password = password };
            await service.RegisterAsync(registerDto, "192.168.1.1");

            var loginDto = new UserDto { Email = "test@example.com", Password = password };
            var result = await service.LoginAsync(loginDto, "192.168.1.2");

            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_RevokesOldRefreshTokens()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var password = "password123";
            var registerDto = new UserDto { Email = "test@example.com", Password = password };
            await service.RegisterAsync(registerDto, "192.168.1.1");

            var oldTokenCount = db.RefreshTokens.Count();
            Assert.Equal(1, oldTokenCount);

            var loginDto = new UserDto { Email = "test@example.com", Password = password };
            await service.LoginAsync(loginDto, "192.168.1.2");

            var revokedCount = db.RefreshTokens.Where(rt => rt.IsRevoked).Count();
            var activeCount = db.RefreshTokens.Where(rt => !rt.IsRevoked).Count();

            Assert.Equal(1, revokedCount);
            Assert.Equal(1, activeCount);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            await service.RegisterAsync(registerDto, "192.168.1.1");

            var loginDto = new UserDto { Email = "test@example.com", Password = "wrongpassword" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(loginDto, "192.168.1.2"));
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_RecordsFailedLoginAttempt()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            await service.RegisterAsync(registerDto, "192.168.1.1");

            var loginDto = new UserDto { Email = "test@example.com", Password = "wrongpassword" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(loginDto, "192.168.1.2"));

            Assert.Single(db.FailedLoginAttempts);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var loginDto = new UserDto { Email = "nonexistent@example.com", Password = "password123" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(loginDto, "192.168.1.1"));
        }

        #endregion

        #region RefreshAsync

        [Fact]
        public async Task RefreshAsync_ValidToken_ReturnsNewAuthResult()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            var registerResult = await service.RegisterAsync(registerDto, "192.168.1.1");

            var refreshResult = await service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2");

            Assert.NotNull(refreshResult.AccessToken);
            Assert.NotNull(refreshResult.RefreshToken);
        }

        [Fact]
        public async Task RefreshAsync_ValidToken_RevokesOldToken()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            var registerResult = await service.RegisterAsync(registerDto, "192.168.1.1");

            var oldToken = await db.RefreshTokens.FirstAsync();
            Assert.False(oldToken.IsRevoked);

            await service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2");

            oldToken = await db.RefreshTokens.Where(rt => rt.Token == oldToken.Token).FirstAsync();
            Assert.True(oldToken.IsRevoked);
        }

        [Fact]
        public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorizedAccessException()
        {
            var db = CreateDbContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);

            var expiredToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = HashToken("expired-token"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.RefreshTokens.Add(expiredToken);
            await db.SaveChangesAsync();

            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync("expired-token", "192.168.1.1"));
        }

        [Fact]
        public async Task RefreshAsync_RevokedToken_ThrowsUnauthorizedAccessException()
        {
            var db = CreateDbContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);

            var revokedToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = HashToken("revoked-token"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.RefreshTokens.Add(revokedToken);
            await db.SaveChangesAsync();

            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync("revoked-token", "192.168.1.1"));
        }

        [Fact]
        public async Task RefreshAsync_NonExistentToken_ThrowsUnauthorizedAccessException()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync("nonexistent-token", "192.168.1.1"));
        }

        #endregion

        #region LogoutAsync

        [Fact]
        public async Task LogoutAsync_WithActiveTokens_RevokesAllTokens()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            var registerResult = await service.RegisterAsync(registerDto, "192.168.1.1");

            var user = await db.Users.FirstAsync();
            var token2 = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "second-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.RefreshTokens.Add(token2);
            await db.SaveChangesAsync();

            Assert.Equal(2, db.RefreshTokens.Where(rt => !rt.IsRevoked).Count());

            await service.LogoutAsync(user.Id);

            Assert.Equal(0, db.RefreshTokens.Where(rt => !rt.IsRevoked).Count());
            Assert.Equal(2, db.RefreshTokens.Where(rt => rt.IsRevoked).Count());
        }

        [Fact]
        public async Task LogoutAsync_WithNoTokens_DoesNotThrow()
        {
            var db = CreateDbContext();
            var rateLimitMock = CreateRateLimitMock();
            var service = new AuthService(db, CreateConfig(), rateLimitMock.Object);

            var userId = Guid.NewGuid();

            await service.LogoutAsync(userId);
            // No exception thrown
        }

        #endregion
    }
}
