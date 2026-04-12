using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/analytics/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class InsightsController(IInsightsService insightsService) : ControllerBase
{
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
    [ProducesResponseType(typeof(IReadOnlyList<TagTrendPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TagTrendPointDto>>> GetTagTrend(
        [FromQuery] Guid tagId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] TrendGranularity period = TrendGranularity.Week)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetTagTrendAsync(userId, tagId, from, to, period));
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

    [HttpGet("top-tags")]
    [ProducesResponseType(typeof(TriggersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TriggersDto>> GetTopTags(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetTriggersAsync(userId, from, to));
    }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(BalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BalanceDto>> GetBalance(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetBalanceAsync(userId, from, to));
    }

    [HttpGet("day-of-week")]
    [ProducesResponseType(typeof(IReadOnlyList<WeekdayStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WeekdayStatsDto>>> GetDayOfWeekBreakdown(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
    {
        if (from > to)
            return BadRequest("'from' must be earlier than 'to'.");

        var userId = User.GetUserId();
        return Ok(await insightsService.GetWeekdayStatsAsync(userId, from, to));
    }
}
