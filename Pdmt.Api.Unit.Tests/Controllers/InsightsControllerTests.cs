using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Controllers;

public class InsightsControllerTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IInsightsService> _insightsService = new();
    private readonly InsightsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public InsightsControllerTests()
    {
        _sut = new InsightsController(_insightsService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())]))
                }
            }
        };
    }

    // ── GetMostIntenseTags ────────────────────────────────────────────────

    [Fact]
    public async Task GetMostIntenseTags_ValidRange_Returns200()
    {
        var expected = new MostIntenseTagsDto([], []);
        _insightsService.Setup(s => s.GetMostIntenseTagsAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetMostIntenseTags(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetMostIntenseTags_FromAfterTo_Returns400()
    {
        var result = await _sut.GetMostIntenseTags(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetMostIntenseTagsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // ── GetRepeatingTriggers ──────────────────────────────────────────────

    [Fact]
    public async Task GetRepeatingTriggers_ValidRange_Returns200()
    {
        IReadOnlyList<RepeatingTriggerDto> expected = [];
        _insightsService.Setup(s => s.GetRepeatingTriggersAsync(_userId, From, To, It.IsAny<int>())).ReturnsAsync(expected);

        var result = await _sut.GetRepeatingTriggers(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetRepeatingTriggers_FromAfterTo_Returns400()
    {
        var result = await _sut.GetRepeatingTriggers(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetRepeatingTriggersAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()), Times.Never);
    }

    // ── GetBalance ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalance_ValidRange_Returns200()
    {
        var expected = new PosNegBalanceDto(3, 2, 7.0, 5.5);
        _insightsService.Setup(s => s.GetBalanceAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetBalance(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetBalance_FromAfterTo_Returns400()
    {
        var result = await _sut.GetBalance(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetBalanceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // ── GetTrends ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrends_ValidRange_Returns200()
    {
        IReadOnlyList<TrendPeriodDto> expected = [];
        _insightsService.Setup(s => s.GetTrendsAsync(_userId, From, To, It.IsAny<Granularity>())).ReturnsAsync(expected);

        var result = await _sut.GetTrends(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTrends_FromAfterTo_Returns400()
    {
        var result = await _sut.GetTrends(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetTrendsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<Granularity>()), Times.Never);
    }

    // ── GetDiscountedPositives ────────────────────────────────────────────

    [Fact]
    public async Task GetDiscountedPositives_ValidRange_Returns200()
    {
        IReadOnlyList<DiscountedPositiveDto> expected = [];
        _insightsService.Setup(s => s.GetDiscountedPositivesAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetDiscountedPositives(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetDiscountedPositives_FromAfterTo_Returns400()
    {
        var result = await _sut.GetDiscountedPositives(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetDiscountedPositivesAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // ── GetWeekdayStats ───────────────────────────────────────────────────

    [Fact]
    public async Task GetWeekdayStats_ValidRange_Returns200()
    {
        IReadOnlyList<WeekdayStatDto> expected = [];
        _insightsService.Setup(s => s.GetWeekdayStatsAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetWeekdayStats(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetWeekdayStats_FromAfterTo_Returns400()
    {
        var result = await _sut.GetWeekdayStats(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetWeekdayStatsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // ── GetNextDayEffects ─────────────────────────────────────────────────

    [Fact]
    public async Task GetNextDayEffects_ValidRange_Returns200()
    {
        IReadOnlyList<NextDayEffectDto> expected = [];
        _insightsService.Setup(s => s.GetNextDayEffectsAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetNextDayEffects(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetNextDayEffects_FromAfterTo_Returns400()
    {
        var result = await _sut.GetNextDayEffects(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetNextDayEffectsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // ── GetTagCombos ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagCombos_ValidRange_Returns200()
    {
        IReadOnlyList<TagComboDto> expected = [];
        _insightsService.Setup(s => s.GetTagCombosAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetTagCombos(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTagCombos_FromAfterTo_Returns400()
    {
        var result = await _sut.GetTagCombos(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetTagCombosAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    // ── GetTagTrend ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagTrend_ValidRange_Returns200()
    {
        IReadOnlyList<TagTrendSeriesDto> expected = [];
        _insightsService.Setup(s => s.GetTagTrendAsync(_userId, From, To, It.IsAny<Granularity>())).ReturnsAsync(expected);

        var result = await _sut.GetTagTrend(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTagTrend_FromAfterTo_Returns400()
    {
        var result = await _sut.GetTagTrend(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetTagTrendAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<Granularity>()), Times.Never);
    }

    // ── GetInfluenceabilitySplit ──────────────────────────────────────────

    [Fact]
    public async Task GetInfluenceabilitySplit_ValidRange_Returns200()
    {
        var expected = new InfluenceabilitySplitDto(5, 6.0, 3, 7.5);
        _insightsService.Setup(s => s.GetInfluenceabilitySplitAsync(_userId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetInfluenceabilitySplit(From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetInfluenceabilitySplit_FromAfterTo_Returns400()
    {
        var result = await _sut.GetInfluenceabilitySplit(To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _insightsService.Verify(s => s.GetInfluenceabilitySplitAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }
}
