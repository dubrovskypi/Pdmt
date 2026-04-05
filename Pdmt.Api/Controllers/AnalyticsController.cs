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

    [HttpGet("trends")]
    [ProducesResponseType(typeof(IReadOnlyList<TrendPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TrendPeriodDto>>> GetTrends(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] TrendGranularity period = TrendGranularity.Week)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetTrendsAsync(userId, from, to, period));
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
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
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

    [HttpGet("insights/repeating-triggers")]
    [ProducesResponseType(typeof(IReadOnlyList<RepeatingTriggerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RepeatingTriggerDto>>> GetRepeatingTriggers(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int minCount = 3)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetRepeatingTriggersAsync(userId, from, to, minCount));
    }

    [HttpGet("insights/discounted-positives")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscountedPositiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DiscountedPositiveDto>>> GetDiscountedPositives(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetDiscountedPositivesAsync(userId, from, to));
    }

    [HttpGet("insights/next-day-effects")]
    [ProducesResponseType(typeof(IReadOnlyList<NextDayEffectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NextDayEffectDto>>> GetNextDayEffects(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetNextDayEffectsAsync(userId, from, to));
    }

    [HttpGet("insights/tag-combos")]
    [ProducesResponseType(typeof(IReadOnlyList<TagComboDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TagComboDto>>> GetTagCombos(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetTagCombosAsync(userId, from, to));
    }

    [HttpGet("insights/tag-trend")]
    [ProducesResponseType(typeof(IReadOnlyList<TagTrendPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TagTrendPointDto>>> GetTagTrend(
        [FromQuery] Guid tagId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] TrendGranularity period = TrendGranularity.Week)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetTagTrendAsync(userId, tagId, from, to, period));
    }

    [HttpGet("insights/influenceability")]
    [ProducesResponseType(typeof(InfluenceabilitySplitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InfluenceabilitySplitDto>> GetInfluenceabilitySplit(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await analyticsService.GetInfluenceabilitySplitAsync(userId, from, to));
    }
}
