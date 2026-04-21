using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests
{
    public class AnalyticsServiceTests
    {
        private readonly AppDbContext _db;
        private readonly AnalyticsService _service;

        public AnalyticsServiceTests()
        {
            _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            _service = new AnalyticsService(_db, new ConfigurationBuilder()
                .AddInMemoryCollection([new("App:DefaultTimeZone", "Europe/Vilnius")])
                .Build());
        }

        #region GetWeeklySummaryAsync

        [Fact]
        public async Task GetWeeklySummaryAsync_NoEvents_ReturnsZeroedSummary()
        {
            var userId = Guid.NewGuid();

            var result = await _service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow));

            Assert.Equal(0, result.PosCount);
            Assert.Equal(0, result.NegCount);
            Assert.Equal(0.0, result.PosToNegRatio);
            Assert.Equal(0.0, result.AvgPosIntensity);
            Assert.Empty(result.TopTags);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_MixedEvents_CountsCorrectly()
        {
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "P1", EventType.Positive, 8, monday),
                TestHelpers.MakeEvent(userId, "P2", EventType.Positive, 7, monday.AddDays(1)),
                TestHelpers.MakeEvent(userId, "P3", EventType.Positive, 9, monday.AddDays(2)),
                TestHelpers.MakeEvent(userId, "N1", EventType.Negative, 5, monday.AddDays(3)),
                TestHelpers.MakeEvent(userId, "N2", EventType.Negative, 6, monday.AddDays(4))
            );
            await _db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await _service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(3, result.PosCount);
            Assert.Equal(2, result.NegCount);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_PosToNegRatio_CalculatedCorrectly()
        {
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "P1", timestamp: monday),
                TestHelpers.MakeEvent(userId, "P2", timestamp: monday),
                TestHelpers.MakeEvent(userId, "P3", timestamp: monday),
                TestHelpers.MakeEvent(userId, "P4", timestamp: monday),
                TestHelpers.MakeEvent(userId, "N1", EventType.Negative, timestamp: monday),
                TestHelpers.MakeEvent(userId, "N2", EventType.Negative, timestamp: monday)
            );
            await _db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await _service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(4.0 / 2.0, result.PosToNegRatio);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_NoNegativeEvents_RatioIsZero()
        {
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "P1", timestamp: monday),
                TestHelpers.MakeEvent(userId, "P2", timestamp: monday)
            );
            await _db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await _service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(0.0, result.PosToNegRatio);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_TopTags_LimitedToFive()
        {
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var tags = Enumerable.Range(0, 7)
                .Select(i => TestHelpers.MakeTag(userId, $"Tag{i}"))
                .ToList();
            _db.Tags.AddRange(tags);

            var events = Enumerable.Range(0, 7)
                .Select(i => TestHelpers.MakeEvent(userId, $"E{i}", timestamp: monday))
                .ToList();
            _db.Events.AddRange(events);

            for (int i = 0; i < 7; i++)
            {
                _db.EventTags.Add(new EventTag { EventId = events[i].Id, TagId = tags[i].Id });
            }
            await _db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await _service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.True(result.TopTags.Count <= 5);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_FiltersOutsideWeek_NotCounted()
        {
            var userId = Guid.NewGuid();

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);
            var sundayBefore = monday.AddDays(-1);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "P1", timestamp: sundayBefore),
                TestHelpers.MakeEvent(userId, "P2", timestamp: monday)
            );
            await _db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await _service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(1, result.PosCount);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_RespectsUserId_OnlyQueriedUserEvents()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId1, "P1", timestamp: monday),
                TestHelpers.MakeEvent(userId2, "P2", timestamp: monday)
            );
            await _db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await _service.GetWeeklySummaryAsync(userId1, weekOf);

            Assert.Equal(1, result.PosCount);
        }

        #endregion

        #region GetCorrelationsAsync

        [Fact]
        public async Task GetCorrelationsAsync_TagNotOwnedByUser_ThrowsNotFoundException()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var tag = TestHelpers.MakeTag(userId2, "Work");
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            var now = DateTimeOffset.UtcNow;
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetCorrelationsAsync(userId1, tag.Id, now, now.AddDays(7)));
        }

        [Fact]
        public async Task GetCorrelationsAsync_SplitsEventsByTagPresence()
        {
            var userId = Guid.NewGuid();

            var tag = TestHelpers.MakeTag(userId, "Work");
            _db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var eventsWithTag = Enumerable.Range(0, 3)
                .Select(i => TestHelpers.MakeEvent(userId, $"With{i}", intensity: 6, timestamp: now.AddHours(i)))
                .ToList();
            var eventsWithout = Enumerable.Range(0, 2)
                .Select(i => TestHelpers.MakeEvent(userId, $"Without{i}", intensity: 4, timestamp: now.AddHours(10 + i)))
                .ToList();

            _db.Events.AddRange(eventsWithTag);
            _db.Events.AddRange(eventsWithout);

            foreach (var ev in eventsWithTag)
            {
                _db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await _db.SaveChangesAsync();

            var result = await _service.GetCorrelationsAsync(userId, tag.Id, now, now.AddDays(1));

            Assert.True(result.AvgIntensityWithTag > 0);
            Assert.True(result.AvgIntensityWithoutTag > 0);
            Assert.True(result.AvgIntensityWithTag > result.AvgIntensityWithoutTag);
        }

        [Fact]
        public async Task GetCorrelationsAsync_NoEventsWithTag_AvgWithTagIsZero()
        {
            var userId = Guid.NewGuid();

            var tag = TestHelpers.MakeTag(userId, "Work");
            _db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var ev = TestHelpers.MakeEvent(userId, "E1");
            _db.Events.Add(ev);
            await _db.SaveChangesAsync();

            var result = await _service.GetCorrelationsAsync(userId, tag.Id, now, now.AddDays(1));

            Assert.Equal(0.0, result.AvgIntensityWithTag);
        }

        #endregion

        #region GetCalendarWeekAsync

        [Fact]
        public async Task GetCalendarWeekAsync_AlwaysReturnsSevenDays()
        {
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var result = await _service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

            Assert.Equal(7, result.Days.Count);
        }

        [Fact]
        public async Task GetCalendarWeekAsync_EmptyDay_HasZeroValues()
        {
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var result = await _service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

            var emptyDay = result.Days.First();
            Assert.Equal(0, emptyDay.PosCount);
            Assert.Equal(0, emptyDay.NegCount);
            Assert.Equal(0.0, emptyDay.DayScore);
        }

        [Fact]
        public async Task GetCalendarWeekAsync_DayScore_CalculatedCorrectly()
        {
            var userId = Guid.NewGuid();

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "P1", intensity: 8, timestamp: monday),
                TestHelpers.MakeEvent(userId, "N1", EventType.Negative, 4, monday)
            );
            await _db.SaveChangesAsync();

            var result = await _service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

            var firstDay = result.Days[0];
            Assert.Equal((8.0 - 4.0) / 2.0, firstDay.DayScore);
        }

        #endregion

        #region GetCalendarMonthAsync

        [Fact]
        public async Task GetCalendarMonthAsync_FebruaryLeapYear_Returns29Days()
        {
            var userId = Guid.NewGuid();

            var result = await _service.GetCalendarMonthAsync(userId, 2024, 2);

            Assert.Equal(29, result.Days.Count);
        }

        [Fact]
        public async Task GetCalendarMonthAsync_EventOnFirstAndLast_BothPresent()
        {
            var userId = Guid.NewGuid();

            var march1 = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
            var march31 = new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "E1", timestamp: march1),
                TestHelpers.MakeEvent(userId, "E2", timestamp: march31)
            );
            await _db.SaveChangesAsync();

            var result = await _service.GetCalendarMonthAsync(userId, 2024, 3);

            Assert.True(result.Days[0].PosCount > 0);
            Assert.True(result.Days[30].PosCount > 0);
        }

        [Fact]
        public async Task GetCalendarMonthAsync_FiltersOtherMonths()
        {
            var userId = Guid.NewGuid();

            var march = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);
            var april = new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero);

            _db.Events.AddRange(
                TestHelpers.MakeEvent(userId, "E1", timestamp: march),
                TestHelpers.MakeEvent(userId, "E2", timestamp: april)
            );
            await _db.SaveChangesAsync();

            var result = await _service.GetCalendarMonthAsync(userId, 2024, 3);

            Assert.Equal(1, result.Days.Where(d => d.PosCount > 0).Count());
        }

        #endregion
    }
}
