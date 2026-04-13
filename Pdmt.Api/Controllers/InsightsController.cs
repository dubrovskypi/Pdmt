using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class InsightsController(IInsightsService insightsService) : ControllerBase
{
    [HttpGet("most-intense-tags")]
    [ProducesResponseType(typeof(MostIntenseTagsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MostIntenseTagsDto>> GetMostIntenseTags(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetMostIntenseTagsAsync(userId, from, to));
    }

    [HttpGet("repeating-triggers")]
    [ProducesResponseType(typeof(IReadOnlyList<RepeatingTriggerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RepeatingTriggerDto>>> GetRepeatingTriggers(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int minCount = 3)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetRepeatingTriggersAsync(userId, from, to, minCount));
    }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(PosNegBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosNegBalanceDto>> GetBalance(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetBalanceAsync(userId, from, to));
    }

    [HttpGet("trends")]
    [ProducesResponseType(typeof(IReadOnlyList<TrendPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TrendPeriodDto>>> GetTrends(
    [FromQuery] DateTimeOffset from,
    [FromQuery] DateTimeOffset to,
    [FromQuery] Granularity period = Granularity.Week)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetTrendsAsync(userId, from, to, period));
    }

    [HttpGet("discounted-positives")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscountedPositiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DiscountedPositiveDto>>> GetDiscountedPositives(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetDiscountedPositivesAsync(userId, from, to));
    }

    [HttpGet("weekday-stats")]
    [ProducesResponseType(typeof(IReadOnlyList<WeekdayStatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WeekdayStatDto>>> GetWeekdayStats(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetWeekdayStatsAsync(userId, from, to));
    }

    [HttpGet("next-day-effects")]
    [ProducesResponseType(typeof(IReadOnlyList<NextDayEffectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NextDayEffectDto>>> GetNextDayEffects(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetNextDayEffectsAsync(userId, from, to));
    }

    [HttpGet("tag-combos")]
    [ProducesResponseType(typeof(IReadOnlyList<TagComboDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TagComboDto>>> GetTagCombos(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetTagCombosAsync(userId, from, to));
    }

    [HttpGet("tag-trend")]
    [ProducesResponseType(typeof(IReadOnlyList<TagTrendSeriesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TagTrendSeriesDto>>> GetTagTrend(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Granularity period = Granularity.Week)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetTagTrendAsync(userId, from, to, period));
    }

    [HttpGet("influenceability")]
    [ProducesResponseType(typeof(InfluenceabilitySplitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InfluenceabilitySplitDto>> GetInfluenceabilitySplit(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetInfluenceabilitySplitAsync(userId, from, to));
    }
}
