using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Controllers;

public class WebAuthControllerTests
{
    private const string AllowedOrigin = "https://app.test";

    private readonly Mock<IAuthService> _authService = new();
    private readonly WebAuthController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public WebAuthControllerTests()
    {
        _sut = new WebAuthController(_authService.Object, BuildConfig())
        {
            ControllerContext = BuildContext(_userId)
        };
    }

    private static IConfiguration BuildConfig(string[]? origins = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = origins?[0] ?? AllowedOrigin
            })
            .Build();

    private static ControllerContext BuildContext(Guid userId, string? refreshTokenCookie = null, string? origin = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())]))
        };
        if (refreshTokenCookie is not null)
            context.Request.Headers.Cookie = $"refreshToken={refreshTokenCookie}";
        if (origin is not null)
            context.Request.Headers.Origin = origin;
        return new ControllerContext { HttpContext = context };
    }

    private WebAuthController BuildSut(string? refreshTokenCookie = null, string? origin = AllowedOrigin) =>
        new(_authService.Object, BuildConfig())
        {
            ControllerContext = BuildContext(_userId, refreshTokenCookie, origin)
        };

    private static AuthResult BuildServiceResult(string refreshToken = "refresh-token") => new(
        "access-token",
        DateTimeOffset.UtcNow.AddHours(1),
        refreshToken,
        DateTimeOffset.UtcNow.AddDays(30));

    #region Register

    [Fact]
    public async Task Register_ValidDto_Returns201()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        _authService.Setup(s => s.RegisterAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(BuildServiceResult());

        var result = await _sut.Register(dto, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Register_ValidDto_ResponseIsWebAuthResultDto()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        var serviceResult = BuildServiceResult();
        _authService.Setup(s => s.RegisterAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(serviceResult);

        var result = await _sut.Register(dto, CancellationToken.None);

        var body = result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<WebAuthResultDto>().Subject;
        body.AccessToken.Should().Be(serviceResult.AccessToken);
        body.AccessTokenExpiresAt.Should().Be(serviceResult.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Register_ValidDto_SetsHttpOnlyCookie()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        _authService.Setup(s => s.RegisterAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(BuildServiceResult("rt-value"));

        await _sut.Register(dto, CancellationToken.None);

        var setCookie = _sut.HttpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refreshToken=rt-value")
            .And.ContainEquivalentOf("httponly")
            .And.ContainEquivalentOf("secure")
            .And.ContainEquivalentOf("samesite=none");
    }

    #endregion

    #region Login

    [Fact]
    public async Task Login_ValidDto_Returns200()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        _authService.Setup(s => s.LoginAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(BuildServiceResult());

        var result = await _sut.Login(dto, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ValidDto_ResponseIsWebAuthResultDto()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        var serviceResult = BuildServiceResult();
        _authService.Setup(s => s.LoginAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(serviceResult);

        var result = await _sut.Login(dto, CancellationToken.None);

        var body = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<WebAuthResultDto>().Subject;
        body.AccessToken.Should().Be(serviceResult.AccessToken);
    }

    [Fact]
    public async Task Login_ValidDto_SetsHttpOnlyCookie()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        _authService.Setup(s => s.LoginAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(BuildServiceResult("rt-value"));

        await _sut.Login(dto, CancellationToken.None);

        var setCookie = _sut.HttpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refreshToken=rt-value")
            .And.ContainEquivalentOf("httponly")
            .And.ContainEquivalentOf("secure")
            .And.ContainEquivalentOf("samesite=none");
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task Refresh_WithCookieAndAllowedOrigin_Returns200()
    {
        var sut = BuildSut(refreshTokenCookie: "old-token", origin: AllowedOrigin);
        _authService.Setup(s => s.RefreshAsync("old-token", "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(BuildServiceResult("new-token"));

        var result = await sut.Refresh(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_WithCookieAndAllowedOrigin_SetsNewCookie()
    {
        var sut = BuildSut(refreshTokenCookie: "old-token", origin: AllowedOrigin);
        _authService.Setup(s => s.RefreshAsync("old-token", "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(BuildServiceResult("new-token"));

        await sut.Refresh(CancellationToken.None);

        var setCookie = sut.HttpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refreshToken=new-token")
            .And.ContainEquivalentOf("httponly");
    }

    [Fact]
    public async Task Refresh_ForbiddenOrigin_Returns403()
    {
        var sut = BuildSut(refreshTokenCookie: "old-token", origin: "https://evil.example.com");

        var result = await sut.Refresh(CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Refresh_NoCookie_ThrowsUnauthorizedAccessException()
    {
        var sut = BuildSut(refreshTokenCookie: null, origin: AllowedOrigin);

        var act = () => sut.Refresh(CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region Logout

    [Fact]
    public async Task Logout_WithCookieAndAllowedOrigin_Returns204()
    {
        var sut = BuildSut(refreshTokenCookie: "rt-value", origin: AllowedOrigin);
        _authService.Setup(s => s.LogoutAsync("rt-value", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await sut.Logout(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Logout_WithCookieAndAllowedOrigin_CallsLogoutWithToken()
    {
        var sut = BuildSut(refreshTokenCookie: "rt-value", origin: AllowedOrigin);
        _authService.Setup(s => s.LogoutAsync("rt-value", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.Logout(CancellationToken.None);

        _authService.Verify(s => s.LogoutAsync("rt-value", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_ForbiddenOrigin_Returns403()
    {
        var sut = BuildSut(refreshTokenCookie: "rt-value", origin: "https://evil.example.com");

        var result = await sut.Logout(CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Logout_NoCookie_Returns204WithoutCallingService()
    {
        var sut = BuildSut(refreshTokenCookie: null, origin: AllowedOrigin);

        var result = await sut.Logout(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _authService.Verify(s => s.LogoutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_WithCookieAndAllowedOrigin_ClearsRefreshCookie()
    {
        var sut = BuildSut(refreshTokenCookie: "rt-value", origin: AllowedOrigin);
        _authService.Setup(s => s.LogoutAsync("rt-value", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.Logout(CancellationToken.None);

        var setCookie = sut.HttpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refreshToken=;");
    }

    [Fact]
    public async Task LogoutAll_AuthenticatedUserWithAllowedOrigin_Returns204()
    {
        var sut = BuildSut(origin: AllowedOrigin);
        _authService.Setup(s => s.LogoutAllAsync(_userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await sut.LogoutAll(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task LogoutAll_ForbiddenOrigin_Returns403()
    {
        var sut = BuildSut(origin: "https://evil.example.com");

        var result = await sut.LogoutAll(CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task LogoutAll_AuthenticatedUserWithAllowedOrigin_ClearsRefreshCookie()
    {
        var sut = BuildSut(origin: AllowedOrigin);
        _authService.Setup(s => s.LogoutAllAsync(_userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.LogoutAll(CancellationToken.None);

        var setCookie = sut.HttpContext.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refreshToken=;");
    }

    #endregion
}
