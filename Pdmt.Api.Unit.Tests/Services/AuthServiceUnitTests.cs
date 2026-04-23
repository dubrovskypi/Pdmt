using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Services;

public class AuthServiceUnitTests
{
    private readonly Mock<IRateLimitService> _rateLimitMock = new();

    private AuthService CreateSut() =>
        new(null!, BuildConfig(), _rateLimitMock.Object);

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-super-secret-key-min-32-chars!!",
                ["Jwt:Issuer"] = "pdmt-test",
                ["Jwt:Audience"] = "pdmt-test",
                ["Jwt:TokenLifetimeMinutes"] = "60",
                ["Jwt:RefreshTokenLifetimeDays"] = "1",
            })
            .Build();

    // ── RegisterAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_RateLimitExceeded_ThrowsRateLimitExceededException()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Register", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Register"));

        Func<Task> act = () => CreateSut().RegisterAsync(new UserDto { Email = "a@b.com", Password = "password123" }, "127.0.0.1");

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
        try { await sut.RegisterAsync(new UserDto { Email = "a@b.com", Password = "password123" }, "127.0.0.1"); }
        catch (RateLimitExceededException) { }

        _rateLimitMock.Verify(r => r.CheckAsync("Auth.Register", "127.0.0.1"), Times.Once);
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_RateLimitExceeded_ThrowsRateLimitExceededException()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Login", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Login"));

        Func<Task> act = () => CreateSut().LoginAsync(new UserDto { Email = "a@b.com", Password = "password123" }, "127.0.0.1");

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Auth.Login*");
    }

    // ── RefreshAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_RateLimitExceeded_ThrowsRateLimitExceededException()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Refresh", It.IsAny<string>()))
            .ThrowsAsync(new RateLimitExceededException("Auth.Refresh"));

        Func<Task> act = () => CreateSut().RefreshAsync("any-token", "127.0.0.1");

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Auth.Refresh*");
    }
}
