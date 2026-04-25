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
        var result = await _service.GetWeeklySummaryAsync(TestAuthHandler.TestUserId, DateOnly.FromDateTime(DateTime.UtcNow));

        result.PosCount.Should().Be(0);
        result.NegCount.Should().Be(0);
        result.PosToNegRatio.Should().Be(0.0);
        result.AvgPosIntensity.Should().Be(0.0);
        result.TopTags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_MixedEvents_CountsCorrectly()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "P1", EventType.Positive, 8, monday),
            TestHelpers.MakeEvent(userId, "P2", EventType.Positive, 7, monday.AddDays(1)),
            TestHelpers.MakeEvent(userId, "P3", EventType.Positive, 9, monday.AddDays(2)),
            TestHelpers.MakeEvent(userId, "N1", EventType.Negative, 5, monday.AddDays(3)),
            TestHelpers.MakeEvent(userId, "N2", EventType.Negative, 6, monday.AddDays(4)));
        await Db.SaveChangesAsync();

        var result = await _service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(monday.DateTime));

        result.PosCount.Should().Be(3);
        result.NegCount.Should().Be(2);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_PosToNegRatio_CalculatedCorrectly()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "P1", timestamp: monday),
            TestHelpers.MakeEvent(userId, "P2", timestamp: monday),
            TestHelpers.MakeEvent(userId, "P3", timestamp: monday),
            TestHelpers.MakeEvent(userId, "P4", timestamp: monday),
            TestHelpers.MakeEvent(userId, "N1", EventType.Negative, timestamp: monday),
            TestHelpers.MakeEvent(userId, "N2", EventType.Negative, timestamp: monday));
        await Db.SaveChangesAsync();

        var result = await _service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(monday.DateTime));

        result.PosToNegRatio.Should().Be(4.0 / 2.0);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_NoNegativeEvents_RatioIsZero()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "P1", timestamp: monday),
            TestHelpers.MakeEvent(userId, "P2", timestamp: monday));
        await Db.SaveChangesAsync();

        var result = await _service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(monday.DateTime));

        result.PosToNegRatio.Should().Be(0.0);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_TopTags_LimitedToFive()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        var tags = Enumerable.Range(0, 7).Select(i => TestHelpers.MakeTag(userId, $"Tag{i}")).ToList();
        Db.Tags.AddRange(tags);
        var events = Enumerable.Range(0, 7).Select(i => TestHelpers.MakeEvent(userId, $"E{i}", timestamp: monday)).ToList();
        Db.Events.AddRange(events);
        for (int i = 0; i < 7; i++)
            Db.EventTags.Add(new EventTag { EventId = events[i].Id, TagId = tags[i].Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(monday.DateTime));

        result.TopTags.Count.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_FiltersOutsideWeek_NotCounted()
    {
        var userId = TestAuthHandler.TestUserId;
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);
        // PostgreSQL stores timestamptz in UTC — convert before seeding
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "P1", timestamp: monday.AddDays(-1).ToUniversalTime()),
            TestHelpers.MakeEvent(userId, "P2", timestamp: monday.ToUniversalTime()));
        await Db.SaveChangesAsync();

        var result = await _service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(monday.DateTime));

        result.PosCount.Should().Be(1);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_RespectsUserId_OnlyQueriedUserEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        Db.Events.AddRange(
            TestHelpers.MakeEvent(TestAuthHandler.TestUserId, "P1", timestamp: monday),
            TestHelpers.MakeEvent(OtherUserId, "P2", timestamp: monday));
        await Db.SaveChangesAsync();

        var result = await _service.GetWeeklySummaryAsync(TestAuthHandler.TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.PosCount.Should().Be(1);
    }

    #endregion

    #region GetCorrelationsAsync

    [Fact]
    public async Task GetCorrelationsAsync_TagNotOwnedByUser_ThrowsNotFoundException()
    {
        var tag = TestHelpers.MakeTag(OtherUserId, "Work");
        Db.Tags.Add(tag);
        await Db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;

        var act = () => _service.GetCorrelationsAsync(TestAuthHandler.TestUserId, tag.Id, now, now.AddDays(7));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetCorrelationsAsync_SplitsEventsByTagPresence()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = TestHelpers.MakeTag(userId, "Work");
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        var eventsWithTag = Enumerable.Range(0, 3)
            .Select(i => TestHelpers.MakeEvent(userId, $"With{i}", intensity: 6, timestamp: now.AddHours(i)))
            .ToList();
        var eventsWithout = Enumerable.Range(0, 2)
            .Select(i => TestHelpers.MakeEvent(userId, $"Without{i}", intensity: 4, timestamp: now.AddHours(10 + i)))
            .ToList();
        Db.Events.AddRange(eventsWithTag);
        Db.Events.AddRange(eventsWithout);
        foreach (var ev in eventsWithTag)
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetCorrelationsAsync(userId, tag.Id, now, now.AddDays(1));

        result.AvgIntensityWithTag.Should().BePositive();
        result.AvgIntensityWithoutTag.Should().BePositive();
        result.AvgIntensityWithTag.Should().BeGreaterThan(result.AvgIntensityWithoutTag);
    }

    [Fact]
    public async Task GetCorrelationsAsync_NoEventsWithTag_AvgWithTagIsZero()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = TestHelpers.MakeTag(userId, "Work");
        Db.Tags.Add(tag);
        Db.Events.Add(TestHelpers.MakeEvent(userId, "E1"));
        await Db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetCorrelationsAsync(userId, tag.Id, now, now.AddDays(1));

        result.AvgIntensityWithTag.Should().Be(0.0);
    }

    #endregion

    #region GetCalendarWeekAsync

    [Fact]
    public async Task GetCalendarWeekAsync_AlwaysReturnsSevenDays()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);

        var result = await _service.GetCalendarWeekAsync(TestAuthHandler.TestUserId, DateOnly.FromDateTime(monday.DateTime));

        result.Days.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetCalendarWeekAsync_EmptyDay_HasZeroValues()
    {
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);

        var result = await _service.GetCalendarWeekAsync(TestAuthHandler.TestUserId, DateOnly.FromDateTime(monday.DateTime));

        var emptyDay = result.Days.First();
        emptyDay.PosCount.Should().Be(0);
        emptyDay.NegCount.Should().Be(0);
        emptyDay.DayScore.Should().Be(0.0);
    }

    [Fact]
    public async Task GetCalendarWeekAsync_DayScore_CalculatedCorrectly()
    {
        var userId = TestAuthHandler.TestUserId;
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);
        // PostgreSQL stores timestamptz in UTC — convert before seeding
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "P1", intensity: 8, timestamp: monday.ToUniversalTime()),
            TestHelpers.MakeEvent(userId, "N1", EventType.Negative, 4, monday.ToUniversalTime()));
        await Db.SaveChangesAsync();

        var result = await _service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

        result.Days[0].DayScore.Should().Be((8.0 - 4.0) / 2.0);
    }

    #endregion

    #region GetCalendarMonthAsync

    [Fact]
    public async Task GetCalendarMonthAsync_FebruaryLeapYear_Returns29Days()
    {
        var result = await _service.GetCalendarMonthAsync(TestAuthHandler.TestUserId, 2024, 2);

        result.Days.Should().HaveCount(29);
    }

    [Fact]
    public async Task GetCalendarMonthAsync_EventOnFirstAndLast_BothPresent()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "E1", timestamp: new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            TestHelpers.MakeEvent(userId, "E2", timestamp: new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero)));
        await Db.SaveChangesAsync();

        var result = await _service.GetCalendarMonthAsync(userId, 2024, 3);

        result.Days[0].PosCount.Should().BePositive();
        result.Days[30].PosCount.Should().BePositive();
    }

    [Fact]
    public async Task GetCalendarMonthAsync_FiltersOtherMonths()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "E1", timestamp: new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero)),
            TestHelpers.MakeEvent(userId, "E2", timestamp: new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero)));
        await Db.SaveChangesAsync();

        var result = await _service.GetCalendarMonthAsync(userId, 2024, 3);

        result.Days.Where(d => d.PosCount > 0).Should().ContainSingle();
    }

    #endregion
}
