using Microsoft.AspNetCore.Mvc;

namespace Pdmt.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummaryController : ControllerBase
    {
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
