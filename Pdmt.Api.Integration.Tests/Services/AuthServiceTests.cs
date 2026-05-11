using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Infrastructure.Metrics;
using Pdmt.Api.Infrastructure.Options;
using Pdmt.Api.Integration.Tests.Infrastructure;
using Pdmt.Api.Services;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Integration.Tests.Services;

public class AuthServiceTests(PostgresContainerFixture fixture) : ServiceTestBase(fixture)
{
    private AuthService _service = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = PostgresWebAppFactory.TestJwtIssuer,
            Audience = PostgresWebAppFactory.TestJwtAudience,
            TokenLifetimeMinutes = 60,
            RefreshTokenLifetimeDays = 1
        });
        SigningCredentials testSigningCreds = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PostgresWebAppFactory.TestJwtSecret)),
            SecurityAlgorithms.HmacSha256);
        _service = new AuthService(Db, jwtOptions, new NoOpRateLimitService(), testSigningCreds,
            new AuthMetrics(new TestMeterFactory()), NullLogger<AuthService>.Instance);
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

        var result = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_CreatesUserInDb()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };

        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleOrDefaultAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        user.Should().NotBeNull();
        user!.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_HashesPassword()
    {
        var password = "password123";
        var dto = new UserDto { Email = "new@example.com", Password = password };

        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        user.PasswordHash.Should().NotBe(password);
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_CreatesRefreshToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };

        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        (await Db.RefreshTokens.CountAsync(rt => rt.UserId == user.Id, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_EmailWithUppercase_NormalizesEmail()
    {
        var dto = new UserDto { Email = "New@EXAMPLE.COM", Password = "password123" };

        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        user.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsValidationException()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var dto2 = new UserDto { Email = "new@example.com", Password = "password456" };

        var act = () => _service.RegisterAsync(dto2, "192.168.1.2", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region LoginAsync

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var result = await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2", TestContext.Current.CancellationToken);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_PreservesOldRefreshTokens()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2", TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        var tokens = await Db.RefreshTokens.AsNoTracking().Where(rt => rt.UserId == user.Id).ToListAsync(TestContext.Current.CancellationToken);
        tokens.Count(rt => rt.IsRevoked).Should().Be(0);
        tokens.Count(rt => !rt.IsRevoked).Should().Be(2);
    }

    [Fact]
    public async Task LoginAsync_TwoSequentialLogins_BothRefreshTokensWork()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var r1 = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var r2 = await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2", TestContext.Current.CancellationToken);

        var refresh1 = await _service.RefreshAsync(r1.RefreshToken, "192.168.1.3", TestContext.Current.CancellationToken);
        var refresh2 = await _service.RefreshAsync(r2.RefreshToken, "192.168.1.4", TestContext.Current.CancellationToken);

        refresh1.AccessToken.Should().NotBeNullOrEmpty();
        refresh2.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_NewLogin_CreatesNewFamily()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2", TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        var familyIds = await Db.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.UserId == user.Id)
            .Select(rt => rt.FamilyId)
            .ToListAsync(TestContext.Current.CancellationToken);
        familyIds.Distinct().Count().Should().Be(2);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var act = () => _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "wrongpassword" }, "192.168.1.2", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_RecordsFailedLoginAttempt()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        await FluentActions
            .Invoking(() => _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "wrongpassword" }, "192.168.1.2", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        (await Db.FailedLoginAttempts.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
    {
        var act = () => _service.LoginAsync(new UserDto { Email = "nonexistent@example.com", Password = "password123" }, "192.168.1.1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region RefreshAsync

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewAuthResult()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        var refreshResult = await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2", TestContext.Current.CancellationToken);

        refreshResult.AccessToken.Should().NotBeNullOrEmpty();
        refreshResult.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RevokesOldToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var oldTokenHash = await Db.RefreshTokens
            .Where(rt => !rt.IsRevoked)
            .Select(rt => rt.Token)
            .FirstAsync(TestContext.Current.CancellationToken);

        await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2", TestContext.Current.CancellationToken);

        var oldToken = await Db.RefreshTokens.AsNoTracking().FirstAsync(rt => rt.Token == oldTokenHash, TestContext.Current.CancellationToken);
        oldToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_AssignsSameFamilyIdToNewToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var originalFamilyId = await Db.RefreshTokens
            .Where(rt => !rt.IsRevoked)
            .Select(rt => rt.FamilyId)
            .FirstAsync(TestContext.Current.CancellationToken);

        await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2", TestContext.Current.CancellationToken);

        var newToken = await Db.RefreshTokens
            .AsNoTracking()
            .Where(rt => !rt.IsRevoked)
            .FirstAsync(TestContext.Current.CancellationToken);
        newToken.FamilyId.Should().Be(originalFamilyId);
    }

    [Fact]
    public async Task RefreshAsync_RevokedTokenWithinGraceWindow_IssuesNewToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var originalFamilyId = await Db.RefreshTokens
            .Where(rt => !rt.IsRevoked)
            .Select(rt => rt.FamilyId)
            .FirstAsync(TestContext.Current.CancellationToken);

        await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2", TestContext.Current.CancellationToken);

        // Reuse the same raw token — within the grace window
        var result = await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.3", TestContext.Current.CancellationToken);

        result.AccessToken.Should().NotBeNullOrEmpty();
        var latestToken = await Db.RefreshTokens
            .AsNoTracking()
            .Where(rt => !rt.IsRevoked)
            .OrderByDescending(rt => rt.CreatedAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        latestToken.FamilyId.Should().Be(originalFamilyId);
    }

    [Fact]
    public async Task RefreshAsync_ReusedTokenAfterGraceWindow_RevokesEntireFamily()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var registerResult = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        await _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.2", TestContext.Current.CancellationToken);

        // Backdate RotatedAt to simulate being outside the grace window
        await Db.RefreshTokens
            .Where(rt => rt.IsRevoked && rt.RotatedAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RotatedAt, DateTimeOffset.UtcNow.AddMinutes(-5)),
            TestContext.Current.CancellationToken);

        var act = () => _service.RefreshAsync(registerResult.RefreshToken, "192.168.1.3", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        var activeCount = await Db.RefreshTokens
            .AsNoTracking()
            .CountAsync(rt => rt.UserId == user.Id && !rt.IsRevoked, TestContext.Current.CancellationToken);
        activeCount.Should().Be(0);
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
            CreatedAt = DateTimeOffset.UtcNow,
            FamilyId = Guid.NewGuid()
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _service.RefreshAsync("expired-token", "192.168.1.1", TestContext.Current.CancellationToken);

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
            CreatedAt = DateTimeOffset.UtcNow,
            FamilyId = Guid.NewGuid()
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _service.RefreshAsync("revoked-token", "192.168.1.1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshAsync_NonExistentToken_ThrowsUnauthorizedAccessException()
    {
        var act = () => _service.RefreshAsync("nonexistent-token", "192.168.1.1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region LogoutAsync / LogoutAllAsync

    [Fact]
    public async Task LogoutAsync_RevokesOnlyMatchingToken()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var r1 = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var r2 = await _service.LoginAsync(new UserDto { Email = "new@example.com", Password = "password123" }, "192.168.1.2", TestContext.Current.CancellationToken);

        await _service.LogoutAsync(r1.RefreshToken, TestContext.Current.CancellationToken);

        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        var tokens = await Db.RefreshTokens.AsNoTracking().Where(rt => rt.UserId == user.Id).ToListAsync(TestContext.Current.CancellationToken);
        tokens.Count(rt => rt.IsRevoked).Should().Be(1);
        tokens.Count(rt => !rt.IsRevoked).Should().Be(1);
    }

    [Fact]
    public async Task LogoutAsync_Idempotent_DoesNotThrowOnSecondCall()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        var r = await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);

        await _service.LogoutAsync(r.RefreshToken, TestContext.Current.CancellationToken);
        var act = () => _service.LogoutAsync(r.RefreshToken, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogoutAllAsync_RevokesAllTokensForUser()
    {
        var dto = new UserDto { Email = "new@example.com", Password = "password123" };
        await _service.RegisterAsync(dto, "192.168.1.1", TestContext.Current.CancellationToken);
        var user = await Db.Users.SingleAsync(u => u.Email == "new@example.com", TestContext.Current.CancellationToken);
        Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("second-token"))),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            FamilyId = Guid.NewGuid()
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.LogoutAllAsync(user.Id, TestContext.Current.CancellationToken);

        var tokens = await Db.RefreshTokens.AsNoTracking().Where(rt => rt.UserId == user.Id).ToListAsync(TestContext.Current.CancellationToken);
        tokens.Count(rt => rt.IsRevoked).Should().Be(2);
        tokens.Count(rt => !rt.IsRevoked).Should().Be(0);
    }

    [Fact]
    public async Task LogoutAllAsync_WithNoTokens_DoesNotThrow()
    {
        var act = () => _service.LogoutAllAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Account lockout

    [Fact]
    public async Task LoginAsync_AfterFiveFailedAttempts_ThrowsLockedOut()
    {
        var email = "lockout@example.com";
        var dto = new UserDto { Email = email, Password = "password123" };
        await _service.RegisterAsync(dto, "1.1.1.1", TestContext.Current.CancellationToken);

        for (var i = 0; i < 5; i++)
        {
            try { await _service.LoginAsync(new UserDto { Email = email, Password = "wrong" }, "1.1.1.1", TestContext.Current.CancellationToken); }
            catch (UnauthorizedAccessException) { }
        }

        var act = () => _service.LoginAsync(new UserDto { Email = email, Password = "password123" }, "1.1.1.1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*locked*");
    }

    [Fact]
    public async Task LoginAsync_AfterFiveFailedAttemptsOutsideWindow_AllowsLogin()
    {
        var email = "lockout-expired@example.com";
        var dto = new UserDto { Email = email, Password = "password123" };
        await _service.RegisterAsync(dto, "1.1.1.1", TestContext.Current.CancellationToken);

        // Seed 5 failed attempts with OccurredAtUtc outside the lockout window (> 15 min ago)
        var oldCutoff = DateTimeOffset.UtcNow.AddMinutes(-16);
        for (var i = 0; i < 5; i++)
        {
            Db.FailedLoginAttempts.Add(new FailedLoginAttempt
            {
                Email = email,
                IpAddress = "1.1.1.1",
                OccurredAtUtc = oldCutoff,
                Reason = "Invalid credentials"
            });
        }
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.LoginAsync(new UserDto { Email = email, Password = "password123" }, "1.1.1.1", TestContext.Current.CancellationToken);

        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    #endregion

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name, options.Version);
        public void Dispose() { }
    }
}
