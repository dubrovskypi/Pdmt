using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Infrastructure.Exceptions;
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

    #region GetMostIntenseTags

    [Fact]
    public async Task GetMostIntenseTags_ValidRange_Returns200()
    {
        var expected = new MostIntenseTagsDto([], []);
        _insightsService.Setup(s => s.GetMostIntenseTagsAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetMostIntenseTags(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetMostIntenseTags_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetMostIntenseTagsAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetMostIntenseTags(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetRepeatingTriggers

    [Fact]
    public async Task GetRepeatingTriggers_ValidRange_Returns200()
    {
        IReadOnlyList<RepeatingTriggerDto> expected = [];
        _insightsService.Setup(s => s.GetRepeatingTriggersAsync(_userId, From, To, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetRepeatingTriggers(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetRepeatingTriggers_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetRepeatingTriggersAsync(_userId, To, From, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetRepeatingTriggers(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetBalance

    [Fact]
    public async Task GetBalance_ValidRange_Returns200()
    {
        var expected = new PosNegBalanceDto(3, 2, 7.0, 5.5);
        _insightsService.Setup(s => s.GetBalanceAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetBalance(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetBalance_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetBalanceAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetBalance(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetTrends

    [Fact]
    public async Task GetTrends_ValidRange_Returns200()
    {
        IReadOnlyList<TrendPeriodDto> expected = [];
        _insightsService.Setup(s => s.GetTrendsAsync(_userId, From, To, It.IsAny<Granularity>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetTrends(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTrends_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetTrendsAsync(_userId, To, From, It.IsAny<Granularity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetTrends(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetDiscountedPositives

    [Fact]
    public async Task GetDiscountedPositives_ValidRange_Returns200()
    {
        IReadOnlyList<DiscountedPositiveDto> expected = [];
        _insightsService.Setup(s => s.GetDiscountedPositivesAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetDiscountedPositives(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetDiscountedPositives_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetDiscountedPositivesAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetDiscountedPositives(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetWeekdayStats

    [Fact]
    public async Task GetWeekdayStats_ValidRange_Returns200()
    {
        IReadOnlyList<WeekdayStatDto> expected = [];
        _insightsService.Setup(s => s.GetWeekdayStatsAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetWeekdayStats(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetWeekdayStats_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetWeekdayStatsAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetWeekdayStats(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetNextDayEffects

    [Fact]
    public async Task GetNextDayEffects_ValidRange_Returns200()
    {
        IReadOnlyList<NextDayEffectDto> expected = [];
        _insightsService.Setup(s => s.GetNextDayEffectsAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetNextDayEffects(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetNextDayEffects_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetNextDayEffectsAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetNextDayEffects(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetTagCombos

    [Fact]
    public async Task GetTagCombos_ValidRange_Returns200()
    {
        IReadOnlyList<TagComboDto> expected = [];
        _insightsService.Setup(s => s.GetTagCombosAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetTagCombos(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTagCombos_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetTagCombosAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetTagCombos(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetTagTrend

    [Fact]
    public async Task GetTagTrend_ValidRange_Returns200()
    {
        IReadOnlyList<TagTrendSeriesDto> expected = [];
        _insightsService.Setup(s => s.GetTagTrendAsync(_userId, From, To, It.IsAny<Granularity>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetTagTrend(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTagTrend_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetTagTrendAsync(_userId, To, From, It.IsAny<Granularity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetTagTrend(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region GetInfluenceabilitySplit

    [Fact]
    public async Task GetInfluenceabilitySplit_ValidRange_Returns200()
    {
        var expected = new InfluenceabilitySplitDto(5, 6.0, 3, 7.5);
        _insightsService.Setup(s => s.GetInfluenceabilitySplitAsync(_userId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetInfluenceabilitySplit(From, To, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetInfluenceabilitySplit_FromAfterTo_ThrowsValidationException()
    {
        _insightsService.Setup(s => s.GetInfluenceabilitySplitAsync(_userId, To, From, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("'from' must be earlier than 'to'."));

        await _sut.Invoking(c => c.GetInfluenceabilitySplit(To, From, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    #endregion
}
