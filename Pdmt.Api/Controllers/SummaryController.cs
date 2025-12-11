using Microsoft.AspNetCore.Mvc;

namespace Pdmt.Api.Controllers
{
    public class SummaryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
