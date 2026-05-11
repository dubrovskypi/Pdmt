using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly AuthController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public AuthControllerTests()
    {
        _sut = new AuthController(_authService.Object)
        {
            ControllerContext = BuildContext(_userId)
        };
    }

    private static ControllerContext BuildContext(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())]))
        }
    };

    private static AuthResult BuildAuthResult() => new(
        "access-token",
        DateTimeOffset.UtcNow.AddHours(1),
        "refresh-token",
        DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task Register_ValidDto_Returns201WithAuthResult()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        var authResult = BuildAuthResult();
        _authService.Setup(s => s.RegisterAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(authResult);

        var result = await _sut.Register(dto, CancellationToken.None);

        var objResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(201);
        var body = objResult.Value.Should().BeOfType<AuthResultDto>().Subject;
        body.AccessToken.Should().Be(authResult.AccessToken);
        body.RefreshToken.Should().Be(authResult.RefreshToken);
    }

    [Fact]
    public async Task Login_ValidDto_Returns200WithAuthResult()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        var authResult = BuildAuthResult();
        _authService.Setup(s => s.LoginAsync(dto, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(authResult);

        var result = await _sut.Login(dto, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<AuthResultDto>()
            .Which.AccessToken.Should().Be(authResult.AccessToken);
    }

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithAuthResult()
    {
        var dto = new RefreshRequestDto { RefreshToken = "old-refresh-token" };
        var authResult = BuildAuthResult();
        _authService.Setup(s => s.RefreshAsync(dto.RefreshToken, "unknown", It.IsAny<CancellationToken>())).ReturnsAsync(authResult);

        var result = await _sut.Refresh(dto, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<AuthResultDto>()
            .Which.AccessToken.Should().Be(authResult.AccessToken);
    }

    [Fact]
    public async Task Logout_ValidToken_Returns204()
    {
        var dto = new RefreshRequestDto { RefreshToken = "refresh-token" };
        _authService.Setup(s => s.LogoutAsync(dto.RefreshToken, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Logout(dto, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task LogoutAll_AuthenticatedUser_Returns204()
    {
        _authService.Setup(s => s.LogoutAllAsync(_userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.LogoutAll(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }
}
