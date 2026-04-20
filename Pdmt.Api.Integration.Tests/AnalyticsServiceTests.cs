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
        private AppDbContext CreateDbContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private IConfiguration CreateConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection([new("App:DefaultTimeZone", "Europe/Vilnius")])
                .Build();

        #region GetWeeklySummaryAsync

        [Fact]
        public async Task GetWeeklySummaryAsync_NoEvents_ReturnsZeroedSummary()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var result = await service.GetWeeklySummaryAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow));

            Assert.Equal(0, result.PosCount);
            Assert.Equal(0, result.NegCount);
            Assert.Equal(0.0, result.PosToNegRatio);
            Assert.Equal(0.0, result.AvgPosIntensity);
            Assert.Empty(result.TopTags);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_MixedEvents_CountsCorrectly()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P1", Intensity = 8 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(1), Type = EventType.Positive, Title = "P2", Intensity = 7 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(2), Type = EventType.Positive, Title = "P3", Intensity = 9 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(3), Type = EventType.Negative, Title = "N1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(4), Type = EventType.Negative, Title = "N2", Intensity = 6 }
            );
            await db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(3, result.PosCount);
            Assert.Equal(2, result.NegCount);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_PosToNegRatio_CalculatedCorrectly()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P2", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P3", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P4", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Negative, Title = "N1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Negative, Title = "N2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(4.0 / 2.0, result.PosToNegRatio);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_NoNegativeEvents_RatioIsZero()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(0.0, result.PosToNegRatio);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_TopTags_LimitedToFive()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var tags = Enumerable.Range(0, 7)
                .Select(i => new Tag { Id = Guid.NewGuid(), Name = $"Tag{i}", UserId = userId, CreatedAt = DateTimeOffset.UtcNow })
                .ToList();
            db.Tags.AddRange(tags);

            var events = Enumerable.Range(0, 7)
                .Select(i => new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = $"E{i}", Intensity = 5 })
                .ToList();
            db.Events.AddRange(events);

            for (int i = 0; i < 7; i++)
            {
                db.EventTags.Add(new EventTag { EventId = events[i].Id, TagId = tags[i].Id });
            }
            await db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.True(result.TopTags.Count <= 5);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_FiltersOutsideWeek_NotCounted()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);
            var sundayBefore = monday.AddDays(-1);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = sundayBefore, Type = EventType.Positive, Title = "P1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await service.GetWeeklySummaryAsync(userId, weekOf);

            Assert.Equal(1, result.PosCount);
        }

        [Fact]
        public async Task GetWeeklySummaryAsync_RespectsUserId_OnlyQueriedUserEvents()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId1, Timestamp = monday, Type = EventType.Positive, Title = "P1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId2, Timestamp = monday, Type = EventType.Positive, Title = "P2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var weekOf = DateOnly.FromDateTime(monday.DateTime);
            var result = await service.GetWeeklySummaryAsync(userId1, weekOf);

            Assert.Equal(1, result.PosCount);
        }

        #endregion

        #region GetCorrelationsAsync

        [Fact]
        public async Task GetCorrelationsAsync_TagNotOwnedByUser_ThrowsNotFoundException()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId2, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            var now = DateTimeOffset.UtcNow;
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.GetCorrelationsAsync(userId1, tag.Id, now, now.AddDays(7)));
        }

        [Fact]
        public async Task GetCorrelationsAsync_SplitsEventsByTagPresence()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var eventsWithTag = Enumerable.Range(0, 3)
                .Select(i => new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"With{i}", Intensity = 6 })
                .ToList();
            var eventsWithout = Enumerable.Range(0, 2)
                .Select(i => new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(10 + i), Type = EventType.Positive, Title = $"Without{i}", Intensity = 4 })
                .ToList();

            db.Events.AddRange(eventsWithTag);
            db.Events.AddRange(eventsWithout);

            foreach (var ev in eventsWithTag)
            {
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetCorrelationsAsync(userId, tag.Id, now, now.AddDays(1));

            Assert.True(result.AvgIntensityWithTag > 0);
            Assert.True(result.AvgIntensityWithoutTag > 0);
            Assert.True(result.AvgIntensityWithTag > result.AvgIntensityWithoutTag);
        }

        [Fact]
        public async Task GetCorrelationsAsync_NoEventsWithTag_AvgWithTagIsZero()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now, Type = EventType.Positive, Title = "E1", Intensity = 5 };
            db.Events.Add(ev);
            await db.SaveChangesAsync();

            var result = await service.GetCorrelationsAsync(userId, tag.Id, now, now.AddDays(1));

            Assert.Equal(0.0, result.AvgIntensityWithTag);
        }

        #endregion

        #region GetCalendarWeekAsync

        [Fact]
        public async Task GetCalendarWeekAsync_AlwaysReturnsSevenDays()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var result = await service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

            Assert.Equal(7, result.Days.Count);
        }

        [Fact]
        public async Task GetCalendarWeekAsync_EmptyDay_HasZeroValues()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var result = await service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

            var emptyDay = result.Days.First();
            Assert.Equal(0, emptyDay.PosCount);
            Assert.Equal(0, emptyDay.NegCount);
            Assert.Equal(0.0, emptyDay.DayScore);
        }

        [Fact]
        public async Task GetCalendarWeekAsync_DayScore_CalculatedCorrectly()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var monday = nowLocal.AddDays(-(int)nowLocal.DayOfWeek + 1);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "P1", Intensity = 8 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Negative, Title = "N1", Intensity = 4 }
            );
            await db.SaveChangesAsync();

            var result = await service.GetCalendarWeekAsync(userId, DateOnly.FromDateTime(monday.DateTime));

            var firstDay = result.Days[0];
            Assert.Equal((8.0 - 4.0) / 2.0, firstDay.DayScore);
        }

        #endregion

        #region GetCalendarMonthAsync

        [Fact]
        public async Task GetCalendarMonthAsync_FebruaryLeapYear_Returns29Days()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var result = await service.GetCalendarMonthAsync(userId, 2024, 2);

            Assert.Equal(29, result.Days.Count);
        }

        [Fact]
        public async Task GetCalendarMonthAsync_EventOnFirstAndLast_BothPresent()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var march1 = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
            var march31 = new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = march1, Type = EventType.Positive, Title = "E1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = march31, Type = EventType.Positive, Title = "E2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var result = await service.GetCalendarMonthAsync(userId, 2024, 3);

            Assert.True(result.Days[0].PosCount > 0);
            Assert.True(result.Days[30].PosCount > 0);
        }

        [Fact]
        public async Task GetCalendarMonthAsync_FiltersOtherMonths()
        {
            var db = CreateDbContext();
            var service = new AnalyticsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var march = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);
            var april = new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = march, Type = EventType.Positive, Title = "E1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = april, Type = EventType.Positive, Title = "E2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var result = await service.GetCalendarMonthAsync(userId, 2024, 3);

            Assert.Equal(1, result.Days.Where(d => d.PosCount > 0).Count());
        }

        #endregion
    }
}
