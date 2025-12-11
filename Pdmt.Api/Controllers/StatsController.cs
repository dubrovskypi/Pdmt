using Microsoft.AspNetCore.Mvc;

namespace Pdmt.Api.Controllers
{
    public class StatsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
