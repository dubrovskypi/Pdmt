using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Domain;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Integration.Tests.Infrastructure;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests.Services;

public class AnalyticsServiceTests : ServiceTestBase
{
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private AnalyticsService _service = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Db.Users.Add(new UserBuilder().WithId(OtherUserId).WithEmail("other@pdmt.dev").Build());
        await Db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("App:DefaultTimeZone", "Europe/Vilnius")])
            .Build();
        _service = new AnalyticsService(Db, config);
    }

    #region GetWeeklySummaryAsync

    [Fact]
    public async Task GetWeeklySummaryAsync_NoEvents_ReturnsZeroedSummary()
    {
        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(DateTime.UtcNow));

        result.PosCount.Should().Be(0);
        result.NegCount.Should().Be(0);
        result.PosToNegRatio.Should().Be(0.0);
        result.AvgPosIntensity.Should().Be(0.0);
        result.TopTags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_MixedEvents_CountsCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("P1").WithIntensity(8).WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P2").WithIntensity(7).WithTimestamp(monday.AddDays(1)).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P3").WithIntensity(9).WithTimestamp(monday.AddDays(2)).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("N1").WithType(EventType.Negative).WithIntensity(5).WithTimestamp(monday.AddDays(3)).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("N2").WithType(EventType.Negative).WithIntensity(6).WithTimestamp(monday.AddDays(4)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.PosCount.Should().Be(3);
        result.NegCount.Should().Be(2);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_PosToNegRatio_CalculatedCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("P1").WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P2").WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P3").WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P4").WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("N1").WithType(EventType.Negative).WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("N2").WithType(EventType.Negative).WithTimestamp(monday).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.PosToNegRatio.Should().Be(4.0 / 2.0);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_NoNegativeEvents_RatioIsZero()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("P1").WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P2").WithTimestamp(monday).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.PosToNegRatio.Should().Be(0.0);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_TopTags_LimitedToFive()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        var tags = Enumerable.Range(0, 7).Select(i => new TagBuilder().WithUserId(TestUserId).WithName($"Tag{i}").Build()).ToList();
        Db.Tags.AddRange(tags);
        var events = Enumerable.Range(0, 7).Select(i => new EventBuilder().WithUserId(TestUserId).WithTitle($"E{i}").WithTimestamp(monday).Build()).ToList();
        Db.Events.AddRange(events);
        for (int i = 0; i < 7; i++)
            Db.EventTags.Add(new EventTag { EventId = events[i].Id, TagId = tags[i].Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.TopTags.Count.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_FiltersOutsideWeek_NotCounted()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);
        // PostgreSQL stores timestamptz in UTC — convert before seeding
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("P1").WithTimestamp(monday.AddDays(-1).ToUniversalTime()).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("P2").WithTimestamp(monday.ToUniversalTime()).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.PosCount.Should().Be(1);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_RespectsUserId_OnlyQueriedUserEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            new EventBuilder().WithTitle("P1").WithTimestamp(monday).Build(),
            new EventBuilder().WithUserId(OtherUserId).WithTitle("P2").WithTimestamp(monday).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetWeeklySummaryAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.PosCount.Should().Be(1);
    }

    #endregion

    #region GetCorrelationsAsync

    [Fact]
    public async Task GetCorrelationsAsync_TagNotOwnedByUser_ThrowsNotFoundException()
    {
        var tag = new TagBuilder().WithUserId(OtherUserId).WithName("Work").Build();
        Db.Tags.Add(tag);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;

        var act = () => _service.GetCorrelationsAsync(TestUserId, tag.Id, now, now.AddDays(7));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetCorrelationsAsync_SplitsEventsByTagPresence()
    {
        var tag = new TagBuilder().WithUserId(TestUserId).WithName("Work").Build();
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        var eventsWithTag = Enumerable.Range(0, 3)
            .Select(i => new EventBuilder().WithUserId(TestUserId).WithTitle($"With{i}").WithIntensity(6).WithTimestamp(now.AddHours(i)).Build())
            .ToList();
        var eventsWithout = Enumerable.Range(0, 2)
            .Select(i => new EventBuilder().WithUserId(TestUserId).WithTitle($"Without{i}").WithIntensity(4).WithTimestamp(now.AddHours(10 + i)).Build())
            .ToList();
        Db.Events.AddRange(eventsWithTag);
        Db.Events.AddRange(eventsWithout);
        foreach (var ev in eventsWithTag)
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetCorrelationsAsync(TestUserId, tag.Id, now, now.AddDays(1));

        result.AvgIntensityWithTag.Should().BePositive();
        result.AvgIntensityWithoutTag.Should().BePositive();
        result.AvgIntensityWithTag.Should().BeGreaterThan(result.AvgIntensityWithoutTag);
    }

    [Fact]
    public async Task GetCorrelationsAsync_NoEventsWithTag_AvgWithTagIsZero()
    {
        var tag = new TagBuilder().WithUserId(TestUserId).WithName("Work").Build();
        Db.Tags.Add(tag);
        Db.Events.Add(new EventBuilder().WithUserId(TestUserId).WithTitle("E1").Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetCorrelationsAsync(TestUserId, tag.Id, now, now.AddDays(1));

        result.AvgIntensityWithTag.Should().Be(0.0);
    }

    #endregion

    #region GetCalendarWeekAsync

    [Fact]
    public async Task GetCalendarWeekAsync_AlwaysReturnsSevenDays()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);

        var result = await _service.GetCalendarWeekAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.Days.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetCalendarWeekAsync_EmptyDay_HasZeroValues()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);

        var result = await _service.GetCalendarWeekAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        var emptyDay = result.Days.First();
        emptyDay.PosCount.Should().Be(0);
        emptyDay.NegCount.Should().Be(0);
        emptyDay.DayScore.Should().Be(0.0);
    }

    [Fact]
    public async Task GetCalendarWeekAsync_DayScore_CalculatedCorrectly()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);
        // PostgreSQL stores timestamptz in UTC — convert before seeding
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("P1").WithIntensity(8).WithTimestamp(monday.ToUniversalTime()).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("N1").WithType(EventType.Negative).WithIntensity(4).WithTimestamp(monday.ToUniversalTime()).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetCalendarWeekAsync(TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.Days[0].DayScore.Should().Be((8.0 - 4.0) / 2.0);
    }

    #endregion

    #region GetCalendarMonthAsync

    [Fact]
    public async Task GetCalendarMonthAsync_FebruaryLeapYear_Returns29Days()
    {
        var result = await _service.GetCalendarMonthAsync(TestUserId, 2024, 2);

        result.Days.Should().HaveCount(29);
    }

    [Fact]
    public async Task GetCalendarMonthAsync_EventOnFirstAndLast_BothPresent()
    {
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("E1").WithTimestamp(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("E2").WithTimestamp(new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetCalendarMonthAsync(TestUserId, 2024, 3);

        result.Days[0].PosCount.Should().BePositive();
        result.Days[30].PosCount.Should().BePositive();
    }

    [Fact]
    public async Task GetCalendarMonthAsync_FiltersOtherMonths()
    {
        Db.Events.AddRange(
            new EventBuilder().WithUserId(TestUserId).WithTitle("E1").WithTimestamp(new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero)).Build(),
            new EventBuilder().WithUserId(TestUserId).WithTitle("E2").WithTimestamp(new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetCalendarMonthAsync(TestUserId, 2024, 3);

        result.Days.Where(d => d.PosCount > 0).Should().ContainSingle();
    }

    #endregion
}
