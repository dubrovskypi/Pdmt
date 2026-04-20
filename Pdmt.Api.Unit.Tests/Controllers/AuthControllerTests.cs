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

    private static AuthResultDto BuildAuthResult() => new()
    {
        AccessToken = "access-token",
        AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        RefreshToken = "refresh-token"
    };

    [Fact]
    public async Task Register_ValidDto_Returns201WithAuthResult()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        var authResult = BuildAuthResult();
        _authService.Setup(s => s.RegisterAsync(dto, "unknown")).ReturnsAsync(authResult);

        var result = await _sut.Register(dto);

        var objResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(201);
        objResult.Value.Should().Be(authResult);
    }

    [Fact]
    public async Task Login_ValidDto_Returns200WithAuthResult()
    {
        var dto = new UserDto { Email = "user@test.com", Password = "password123" };
        var authResult = BuildAuthResult();
        _authService.Setup(s => s.LoginAsync(dto, "unknown")).ReturnsAsync(authResult);

        var result = await _sut.Login(dto);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(authResult);
    }

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithAuthResult()
    {
        var dto = new RefreshRequestDto { RefreshToken = "old-refresh-token" };
        var authResult = BuildAuthResult();
        _authService.Setup(s => s.RefreshAsync(dto.RefreshToken, "unknown")).ReturnsAsync(authResult);

        var result = await _sut.Refresh(dto);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(authResult);
    }

    [Fact]
    public async Task Logout_AuthenticatedUser_Returns204()
    {
        _authService.Setup(s => s.LogoutAsync(_userId)).Returns(Task.CompletedTask);

        var result = await _sut.Logout();

        result.Should().BeOfType<NoContentResult>();
    }
}
