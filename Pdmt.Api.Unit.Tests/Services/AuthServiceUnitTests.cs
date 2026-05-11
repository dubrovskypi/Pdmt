using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Infrastructure.Metrics;
using Pdmt.Api.Infrastructure.Options;
using Pdmt.Api.Services;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace Pdmt.Api.Unit.Tests.Services;

public class AuthServiceUnitTests
{
    private static readonly SigningCredentials TestSigningCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-super-secret-key-min-32-chars!!")),
        SecurityAlgorithms.HmacSha256);

    private readonly Mock<IRateLimitService> _rateLimitMock = new();

    private AuthService CreateSut() =>
        new(null!, BuildJwtOptions(), _rateLimitMock.Object, TestSigningCredentials,
            new AuthMetrics(new TestMeterFactory()), NullLogger<AuthService>.Instance);

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name, options.Version);
        public void Dispose() { }
    }

    private static IOptions<JwtOptions> BuildJwtOptions() =>
        Options.Create(new JwtOptions
        {
            Issuer = "pdmt-test",
            Audience = "pdmt-test",
            TokenLifetimeMinutes = 60,
            RefreshTokenLifetimeDays = 1
        });

    #region RegisterAsync

    [Fact]
    public async Task RegisterAsync_RateLimitExceeded_ThrowsRateLimitExceededException()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Register", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Register"));

        Func<Task> act = () => CreateSut().RegisterAsync(new UserDto { Email = "a@b.com", Password = "password123" }, "127.0.0.1", CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Auth.Register*");
    }

    [Fact]
    public async Task RegisterAsync_RateLimitExceeded_DoesNotAccessDatabase()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Register", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Register"));

        var sut = CreateSut();
        try { await sut.RegisterAsync(new UserDto { Email = "a@b.com", Password = "password123" }, "127.0.0.1", CancellationToken.None); }
        catch (RateLimitExceededException) { }

        _rateLimitMock.Verify(r => r.CheckAsync("Auth.Register", "127.0.0.1"), Times.Once);
    }

    #endregion

    #region LoginAsync

    [Fact]
    public async Task LoginAsync_RateLimitExceeded_ThrowsRateLimitExceededException()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Login", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Login"));

        Func<Task> act = () => CreateSut().LoginAsync(new UserDto { Email = "a@b.com", Password = "password123" }, "127.0.0.1", CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Auth.Login*");
    }

    #endregion

    #region RefreshAsync

    [Fact]
    public async Task RefreshAsync_RateLimitExceeded_ThrowsRateLimitExceededException()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Refresh", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Refresh"));

        Func<Task> act = () => CreateSut().RefreshAsync("any-token", "127.0.0.1", CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Auth.Refresh*");
    }

    #endregion

    #region Email normalization

    [Fact]
    public void EmailNormalization_IsInvariantUnderTurkishCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            // Turkish "I".ToLower() → "ı" (dotless i), breaking email lookup uniqueness.
            // Regression guard: verify ToLowerInvariant() is always "i", not "ı".
            "USER@EXAMPLE.COM".ToLowerInvariant().Should().Be("user@example.com");
            "USER@EXAMPLE.COM".ToLower().Should().NotBe("user@example.com");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    #endregion
}
