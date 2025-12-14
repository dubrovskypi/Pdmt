using Microsoft.AspNetCore.Mvc;

namespace Pdmt.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummaryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateDailySummary()
        {
            // Logic to create daily summary
            return Ok();
        }
        [HttpGet]
        public IActionResult GetDailySummaries(DateTime from, DateTime to)
        {
            // Logic to get daily summaries for the specified period
            return Ok();
        }
    }
}
