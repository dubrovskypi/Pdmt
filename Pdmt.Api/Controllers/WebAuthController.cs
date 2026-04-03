using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers
{
    /// <summary>
    /// Auth endpoints for browser SPA clients (React).
    /// Uses httpOnly cookie for refresh token — never exposes it in response body.
    /// </summary>
    [ApiController]
    [Route("api/auth/web")]
    public class WebAuthController(IAuthService auth) : ControllerBase
    {
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(WebAuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebAuthResultDto>> Login(UserDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await auth.LoginAsync(dto, ip);
            SetRefreshCookie(result.RefreshToken);
            return Ok(new WebAuthResultDto(result.AccessToken, result.AccessTokenExpiresAt));
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(WebAuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebAuthResultDto>> Refresh()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var token = Request.Cookies["refreshToken"]
                ?? throw new UnauthorizedAccessException("No refresh token cookie");
            var result = await auth.RefreshAsync(token, ip);
            SetRefreshCookie(result.RefreshToken);
            return Ok(new WebAuthResultDto(result.AccessToken, result.AccessTokenExpiresAt));
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            await auth.LogoutAsync(User.GetUserId());
            ClearRefreshCookie();
            return NoContent();
        }

        private void SetRefreshCookie(string token) =>
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(1)
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
