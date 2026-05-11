using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers
{
    /// <summary>
    /// Auth endpoints for browser SPA clients (React).
    /// Uses httpOnly cookie for refresh token — never exposes it in response body.
    /// Origin header is validated against Cors:AllowedOrigins on cookie-mutating endpoints
    /// to prevent CSRF-triggered token rotation or logout from third-party pages.
    /// </summary>
    [ApiController]
    [Route("api/auth/web")]
    public class WebAuthController(IAuthService auth, IConfiguration config) : ControllerBase
    {
        private IReadOnlyList<string> AllowedOrigins =>
            config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        private bool IsOriginAllowed() =>
            OriginValidator.IsAllowed(Request.Headers.Origin.ToString(), AllowedOrigins);
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(WebAuthResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<WebAuthResultDto>> Register(UserDto dto, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await auth.RegisterAsync(dto, ip, ct);
            SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
            return StatusCode(StatusCodes.Status201Created,
                new WebAuthResultDto(result.AccessToken, result.AccessTokenExpiresAt));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(WebAuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebAuthResultDto>> Login(UserDto dto, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await auth.LoginAsync(dto, ip, ct);
            SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
            return Ok(new WebAuthResultDto(result.AccessToken, result.AccessTokenExpiresAt));
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(WebAuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<WebAuthResultDto>> Refresh(CancellationToken ct)
        {
            if (!IsOriginAllowed()) return Forbid();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var token = Request.Cookies["refreshToken"]
                ?? throw new UnauthorizedAccessException("No refresh token cookie");
            var result = await auth.RefreshAsync(token, ip, ct);
            SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
            return Ok(new WebAuthResultDto(result.AccessToken, result.AccessTokenExpiresAt));
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            if (!IsOriginAllowed()) return Forbid();
            var token = Request.Cookies["refreshToken"];
            if (token is not null)
                await auth.LogoutAsync(token, ct);
            ClearRefreshCookie();
            return NoContent();
        }

        [HttpPost("logout-all")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> LogoutAll(CancellationToken ct)
        {
            if (!IsOriginAllowed()) return Forbid();
            await auth.LogoutAllAsync(User.GetUserId(), ct);
            ClearRefreshCookie();
            return NoContent();
        }

        private void SetRefreshCookie(string token, DateTimeOffset expiresAt) =>
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = expiresAt
            });

        private void ClearRefreshCookie() =>
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
    }
}
