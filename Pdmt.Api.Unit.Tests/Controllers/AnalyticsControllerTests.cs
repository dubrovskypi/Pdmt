using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Controllers;

public class AnalyticsControllerTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAnalyticsService> _analyticsService = new();
    private readonly AnalyticsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public AnalyticsControllerTests()
    {
        _sut = new AnalyticsController(_analyticsService.Object)
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

    [Fact]
    public async Task GetWeeklySummary_ValidWeek_Returns200()
    {
        var weekOf = new DateOnly(2026, 1, 5);
        var expected = new WeeklySummaryDto(0, 0, 0.0, 0.0, 0.0, [], [], [], []);
        _analyticsService.Setup(s => s.GetWeeklySummaryAsync(_userId, weekOf)).ReturnsAsync(expected);

        var result = await _sut.GetWeeklySummary(weekOf);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetCorrelations_ValidRange_Returns200()
    {
        var tagId = Guid.NewGuid();
        var expected = new CorrelationsDto("stress", 6.5, 5.0, []);
        _analyticsService.Setup(s => s.GetCorrelationsAsync(_userId, tagId, From, To)).ReturnsAsync(expected);

        var result = await _sut.GetCorrelations(tagId, From, To);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetCorrelations_FromAfterTo_Returns400()
    {
        var result = await _sut.GetCorrelations(Guid.NewGuid(), To, From);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _analyticsService.Verify(
            s => s.GetCorrelationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCalendarWeek_ValidWeek_Returns200()
    {
        var weekOf = new DateOnly(2026, 1, 5);
        var expected = new CalendarWeekDto(From, To, []);
        _analyticsService.Setup(s => s.GetCalendarWeekAsync(_userId, weekOf)).ReturnsAsync(expected);

        var result = await _sut.GetCalendarWeek(weekOf);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetCalendarMonth_ValidFormat_Returns200()
    {
        var expected = new CalendarMonthDto([]);
        _analyticsService.Setup(s => s.GetCalendarMonthAsync(_userId, 2026, 1)).ReturnsAsync(expected);

        var result = await _sut.GetCalendarMonth("2026-01");

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetCalendarMonth_InvalidFormat_Returns400()
    {
        var result = await _sut.GetCalendarMonth("2026/01");

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _analyticsService.Verify(
            s => s.GetCalendarMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }
}
