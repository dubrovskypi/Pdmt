using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResultDto>> Register(UserDto dto)
        {
            return await _auth.RegisterAsync(dto);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResultDto>> Login(UserDto dto)
        {
            return await _auth.LoginAsync(dto);
        }

        [HttpGet]
        public IActionResult Me()
        {
            return View();
        }

    }
}
