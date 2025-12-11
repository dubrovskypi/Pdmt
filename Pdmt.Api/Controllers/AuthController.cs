using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;

namespace Pdmt.Api.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Me()
        {
            return View();
        }

    }
}
