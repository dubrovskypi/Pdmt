using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("weekly-summary")]
    [ProducesResponseType(typeof(WeeklySummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WeeklySummaryDto>> GetWeeklySummary([FromQuery] DateOnly weekOf)
    {
        var userId = User.GetUserId();
        return Ok(await analyticsService.GetWeeklySummaryAsync(userId, weekOf));
    }

    [HttpGet("correlations")]
    [ProducesResponseType(typeof(CorrelationsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CorrelationsDto>> GetCorrelations(
        [FromQuery] Guid tagId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetCorrelationsAsync(userId, tagId, from, to));
    }

    [HttpGet("calendar/week")]
    [ProducesResponseType(typeof(CalendarWeekDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarWeekDto>> GetCalendarWeek([FromQuery] DateOnly weekOf)
    {
        var userId = User.GetUserId();
        return Ok(await analyticsService.GetCalendarWeekAsync(userId, weekOf));
    }

    [HttpGet("calendar/month")]
    [ProducesResponseType(typeof(CalendarMonthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarMonthDto>> GetCalendarMonth([FromQuery] string month)
    {
        if (!DateTime.TryParseExact(month, "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return BadRequest("'month' must be in format yyyy-MM (e.g. 2026-03).");
        }

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetCalendarMonthAsync(userId, parsed.Year, parsed.Month));
    }

}
