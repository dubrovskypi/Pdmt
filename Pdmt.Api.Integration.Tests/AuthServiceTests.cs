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
        private readonly AppDbContext _db;
        private readonly AuthService _service;

        public AuthServiceTests()
        {
            _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            var rateLimitMock = new Mock<IRateLimitService>();
            rateLimitMock.Setup(x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _service = new AuthService(_db, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "test-secret-key-long-enough-32chars!",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                    ["Jwt:TokenLifetimeMinutes"] = "60",
                    ["Jwt:RefreshTokenLifetimeDays"] = "7"
                })
                .Build(), rateLimitMock.Object);
        }

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        #region RegisterAsync

        [Fact]
        public async Task RegisterAsync_ValidCredentials_ReturnsAuthResult()
        {
            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            var result = await _service.RegisterAsync(dto, "192.168.1.1");

            Assert.NotNull(result.AccessToken);
            Assert.NotEmpty(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
            Assert.NotEmpty(result.RefreshToken);
        }

        [Fact]
        public async Task RegisterAsync_ValidCredentials_CreatesUserInDb()
        {
            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            await _service.RegisterAsync(dto, "192.168.1.1");

            Assert.Single(_db.Users);
            var user = await _db.Users.FirstAsync();
            Assert.Equal("test@example.com", user.Email);
        }

        [Fact]
        public async Task RegisterAsync_ValidCredentials_HashesPassword()
        {
            var password = "password123";
            var dto = new UserDto { Email = "test@example.com", Password = password };
            await _service.RegisterAsync(dto, "192.168.1.1");

            var user = await _db.Users.FirstAsync();
            Assert.NotEqual(password, user.PasswordHash);
        }

        [Fact]
        public async Task RegisterAsync_ValidCredentials_CreatesRefreshToken()
        {
            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            await _service.RegisterAsync(dto, "192.168.1.1");

            Assert.Single(_db.RefreshTokens);
        }

        [Fact]
        public async Task RegisterAsync_EmailWithUppercase_NormalizesEmail()
        {
            var dto = new UserDto { Email = "Test@EXAMPLE.COM", Password = "password123" };
            await _service.RegisterAsync(dto, "192.168.1.1");

            var user = await _db.Users.FirstAsync();
            Assert.Equal("test@example.com", user.Email);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
        {
            var dto = new UserDto { Email = "test@example.com", Password = "password123" };
            await _service.RegisterAsync(dto, "192.168.1.1");

            var dto2 = new UserDto { Email = "test@example.com", Password = "password456" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(dto2, "192.168.1.2"));
        }

        #endregion

        #region LoginAsync

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
        {
            var password = "password123";
            var registerDto = new UserDto { Email = "test@example.com", Password = password };
            await _service.RegisterAsync(registerDto, "192.168.1.1");

            var loginDto = new UserDto { Email = "test@example.com", Password = password };
            var result = await _service.LoginAsync(loginDto, "192.168.1.2");

            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_RevokesOldRefreshTokens()
        {
            var password = "password123";
            var registerDto = new UserDto { Email = "test@example.com", Password = password };
            await _service.RegisterAsync(registerDto, "192.168.1.1");

            var oldTokenCount = _db.RefreshTokens.Count();
            Assert.Equal(1, oldTokenCount);

            var loginDto = new UserDto { Email = "test@example.com", Password = password };
            await _service.LoginAsync(loginDto, "192.168.1.2");

            var revokedCount = _db.RefreshTokens.Where(rt => rt.IsRevoked).Count();
            var activeCount = _db.RefreshTokens.Where(rt => !rt.IsRevoked).Count();

            Assert.Equal(1, revokedCount);
            Assert.Equal(1, activeCount);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
        {
            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            await _service.RegisterAsync(registerDto, "192.168.1.1");

            var loginDto = new UserDto { Email = "test@example.com", Password = "wrongpassword" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.LoginAsync(loginDto, "192.168.1.2"));
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_RecordsFailedLoginAttempt()
        {
            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            await _service.RegisterAsync(registerDto, "192.168.1.1");

            var loginDto = new UserDto { Email = "test@example.com", Password = "wrongpassword" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.LoginAsync(loginDto, "192.168.1.2"));

            Assert.Single(_db.FailedLoginAttempts);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
        {
            var loginDto = new UserDto { Email = "nonexistent@example.com", Password = "password123" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.LoginAsync(loginDto, "192.168.1.1"));
        }

        #endregion

        #region RefreshAsync

        [Fact]
        public async Task RefreshAsync_ValidToken_ReturnsNewAuthResult()
        {
            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            var registerResult = await _service.RegisterAsync(registerDto, "192.168.1.1");

            var refreshResult = await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2");

            Assert.NotNull(refreshResult.AccessToken);
            Assert.NotNull(refreshResult.RefreshToken);
        }

        [Fact]
        public async Task RefreshAsync_ValidToken_RevokesOldToken()
        {
            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            var registerResult = await _service.RegisterAsync(registerDto, "192.168.1.1");

            var oldToken = await _db.RefreshTokens.FirstAsync();
            Assert.False(oldToken.IsRevoked);

            await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2");

            oldToken = await _db.RefreshTokens.Where(rt => rt.Token == oldToken.Token).FirstAsync();
            Assert.True(oldToken.IsRevoked);
        }

        [Fact]
        public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorizedAccessException()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);

            var expiredToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = HashToken("expired-token"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.RefreshTokens.Add(expiredToken);
            await _db.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("expired-token", "192.168.1.1"));
        }

        [Fact]
        public async Task RefreshAsync_RevokedToken_ThrowsUnauthorizedAccessException()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);

            var revokedToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = HashToken("revoked-token"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.RefreshTokens.Add(revokedToken);
            await _db.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("revoked-token", "192.168.1.1"));
        }

        [Fact]
        public async Task RefreshAsync_NonExistentToken_ThrowsUnauthorizedAccessException()
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("nonexistent-token", "192.168.1.1"));
        }

        #endregion

        #region LogoutAsync

        [Fact]
        public async Task LogoutAsync_WithActiveTokens_RevokesAllTokens()
        {
            var registerDto = new UserDto { Email = "test@example.com", Password = "password123" };
            var registerResult = await _service.RegisterAsync(registerDto, "192.168.1.1");

            var user = await _db.Users.FirstAsync();
            var token2 = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "second-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.RefreshTokens.Add(token2);
            await _db.SaveChangesAsync();

            Assert.Equal(2, _db.RefreshTokens.Where(rt => !rt.IsRevoked).Count());

            await _service.LogoutAsync(user.Id);

            Assert.Equal(0, _db.RefreshTokens.Where(rt => !rt.IsRevoked).Count());
            Assert.Equal(2, _db.RefreshTokens.Where(rt => rt.IsRevoked).Count());
        }

        [Fact]
        public async Task LogoutAsync_WithNoTokens_DoesNotThrow()
        {
            var userId = Guid.NewGuid();

            await _service.LogoutAsync(userId);
            // No exception thrown
        }

        #endregion
    }
}
