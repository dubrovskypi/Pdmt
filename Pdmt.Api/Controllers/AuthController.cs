using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService auth) : ControllerBase
    {
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResultDto>> Register(UserDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(dto, ip));
        }

        //TODO TLS everywhere
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResultDto>> Login(UserDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return Ok(await auth.LoginAsync(dto, ip));
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResultDto>> Refresh(RefreshRequestDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return Ok(await auth.RefreshAsync(dto.RefreshToken, ip));
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            await auth.LogoutAsync(User.GetUserId());
            return NoContent();
        }
    }
}
