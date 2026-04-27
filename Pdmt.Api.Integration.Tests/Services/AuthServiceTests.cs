using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using Pdmt.Api.Services;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Integration.Tests.Services;

public class AuthServiceTests(PostgresContainerFixture fixture) : ServiceTestBase(fixture)
{
    private AuthService _service = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("Jwt:Issuer", PostgresWebAppFactory.TestJwtIssuer),
                new("Jwt:Audience", PostgresWebAppFactory.TestJwtAudience),
                new("Jwt:TokenLifetimeMinutes", "60"),
                new("Jwt:RefreshTokenLifetimeDays", "1")
            ])
            .Build();
        SigningCredentials testSigningCreds = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PostgresWebAppFactory.TestJwtSecret)),
            SecurityAlgorithms.HmacSha256);
        _service = new AuthService(Db, config, new NoOpRateLimitService(), testSigningCreds);
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
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };

        var result = await _service.RegisterAsync(dto, "192.168.1.1");

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_CreatesUserInDb()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };

        await _service.RegisterAsync(dto, "192.168.1.1");

        var user = await Db.Users.SingleOrDefaultAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        user.Should().NotBeNull();
        user!.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_HashesPassword()
    {
        var password = "password123";
        var dto = new UserDto { Email = "new@example.com", Password = password };

        await _service.RegisterAsync(dto, "192.168.1.1");

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        user.PasswordHash.Should().NotBe(password);
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_CreatesRefreshToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };

        await _service.RegisterAsync(dto, "192.168.1.1");

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        (await Db.RefreshTokens.CountAsync(rt => rt.UserId == user.Id, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_EmailWithUppercase_NormalizesEmail()
    {
        var dto = new UserDto { Email = "New@EXAMPLE.COM", Password = "password123" };

        await _service.RegisterAsync(dto, "192.168.1.1");

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        user.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1");
        var dto2 = new UserDto { Email = "new@example.com", Password = "password456" };

        var act = () => _service.RegisterAsync(dto2, "192.168.1.2");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region LoginAsync

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1");

        var result = await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2");

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_RevokesOldRefreshTokens()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1");

        await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2");

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        var tokens = await Db.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync(TestContext.Current.CancellationToken);
        tokens.Count(rt => rt.IsRevoked).Should().Be(1);
        tokens.Count(rt => !rt.IsRevoked).Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1");

        var act = () => _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "wrongpassword" }, "192.168.1.2");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_RecordsFailedLoginAttempt()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1");

        await FluentActions
            .Invoking(() => _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "wrongpassword" }, "192.168.1.2"))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        (await Db.FailedLoginAttempts.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
    {
        var act = () => _service.LoginAsync(new UserDto { Email = "nonexistent@example.com", Password = "password123" }, "192.168.1.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region RefreshAsync

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewAuthResult()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1");

        var refreshResult = await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2");

        refreshResult.AccessToken.Should().NotBeNullOrEmpty();
        refreshResult.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RevokesOldToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1");
        var oldTokenHash = await Db.RefreshTokens
            .Where(rt => !rt.IsRevoked)
            .Select(rt => rt.Token)
            .FirstAsync(TestContext.Current.CancellationToken);

        await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2");

        var oldToken = await Db.RefreshTokens.FirstAsync(rt => rt.Token == oldTokenHash, TestContext.Current.CancellationToken);
        oldToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Token = HashToken("expired-token"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _service.RefreshAsync("expired-token", "192.168.1.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ThrowsUnauthorizedAccessException()
    {
        Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Token = HashToken("revoked-token"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _service.RefreshAsync("revoked-token", "192.168.1.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshAsync_NonExistentToken_ThrowsUnauthorizedAccessException()
    {
        var act = () => _service.RefreshAsync("nonexistent-token", "192.168.1.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region LogoutAsync

    [Fact]
    public async Task LogoutAsync_WithActiveTokens_RevokesAllTokens()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1");
        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("second-token"))),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.LogoutAsync(user.Id);

        var tokens = await Db.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync(TestContext.Current.CancellationToken);
        tokens.Count(rt => rt.IsRevoked).Should().Be(2);
        tokens.Count(rt => !rt.IsRevoked).Should().Be(0);
    }

    [Fact]
    public async Task LogoutAsync_WithNoTokens_DoesNotThrow()
    {
        var act = () => _service.LogoutAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    #endregion
}
