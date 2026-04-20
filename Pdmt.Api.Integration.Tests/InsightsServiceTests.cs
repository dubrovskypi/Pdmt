using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests
{
    public class InsightsServiceTests
    {
        private AppDbContext CreateDbContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private IConfiguration CreateConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection([new("App:DefaultTimeZone", "Europe/Vilnius")])
                .Build();

        #region GetRepeatingTriggersAsync

        [Fact]
        public async Task GetRepeatingTriggersAsync_TagWithExactlyMinCount_IsIncluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

            Assert.Single(result);
            Assert.Equal("Work", result[0].TagName);
        }

        [Fact]
        public async Task GetRepeatingTriggersAsync_TagBelowMinCount_IsExcluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 2; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRepeatingTriggersAsync_OnlyCountsNegativeEvents()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            // 4 positive + 2 negative, but only negative count
            for (int i = 0; i < 4; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            for (int i = 0; i < 2; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRepeatingTriggersAsync_OrderedByAvgIntensityDescending()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag1 = new Tag { Id = Guid.NewGuid(), Name = "HighIntensity", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            var tag2 = new Tag { Id = Guid.NewGuid(), Name = "LowIntensity", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.AddRange(tag1, tag2);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 3; i++)
            {
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Negative, Title = $"H{i}", Intensity = 9 };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(10 + i), Type = EventType.Negative, Title = $"L{i}", Intensity = 3 };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
                db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

            Assert.Equal(2, result.Count);
            Assert.Equal("HighIntensity", result[0].TagName);
            Assert.Equal("LowIntensity", result[1].TagName);
        }

        [Fact]
        public async Task GetRepeatingTriggersAsync_NoEvents_ReturnsEmpty()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var result = await service.GetRepeatingTriggersAsync(userId, now, now.AddDays(7), minCount: 3);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRepeatingTriggersAsync_FiltersOutsideDateRange()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var start = DateTimeOffset.UtcNow;
            var end = start.AddDays(3);
            var outside = end.AddDays(1);

            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = start.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }

            var outsideEv = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = outside, Type = EventType.Negative, Title = "Outside", Intensity = 5 };
            db.Events.Add(outsideEv);
            db.EventTags.Add(new EventTag { EventId = outsideEv.Id, TagId = tag.Id });

            await db.SaveChangesAsync();

            var result = await service.GetRepeatingTriggersAsync(userId, start, end, minCount: 3);

            Assert.Single(result);
            Assert.Equal(3, result[0].Count);
        }

        [Fact]
        public async Task GetRepeatingTriggersAsync_IsolatesByUserId()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId1, CreatedAt = DateTimeOffset.UtcNow };
            var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId2, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.AddRange(tag1, tag2);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 3; i++)
            {
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId1, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"U1-{i}", Intensity = 5 };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId2, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"U2-{i}", Intensity = 5 };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
                db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetRepeatingTriggersAsync(userId1, now, now.AddDays(7), minCount: 3);

            Assert.Single(result);
            Assert.Equal("Work", result[0].TagName);
        }

        #endregion

        #region GetDiscountedPositivesAsync

        [Fact]
        public async Task GetDiscountedPositivesAsync_TagWith5EventsAvgBelow4_IsIncluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 3 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetDiscountedPositivesAsync(userId, now, now.AddDays(1));

            Assert.Single(result);
            Assert.Equal("Achievement", result[0].TagName);
        }

        [Fact]
        public async Task GetDiscountedPositivesAsync_TagWith4Events_IsExcluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 4; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 3 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetDiscountedPositivesAsync(userId, now, now.AddDays(1));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDiscountedPositivesAsync_TagWithAvgExactly4_IsExcluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 4 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetDiscountedPositivesAsync(userId, now, now.AddDays(1));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDiscountedPositivesAsync_OnlyPositiveEvents_Considered()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            // 3 positive + 5 negative with same tag
            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 3 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            for (int i = 0; i < 5; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 2 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetDiscountedPositivesAsync(userId, now, now.AddDays(7));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDiscountedPositivesAsync_EmptyResult_WhenNoneQualify()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var result = await service.GetDiscountedPositivesAsync(userId, now, now.AddDays(7));

            Assert.Empty(result);
        }

        #endregion

        #region GetNextDayEffectsAsync

        [Fact]
        public async Task GetNextDayEffectsAsync_TagOnExactly3Days_IsIncluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Exercise", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var dayStart = now.Date;
            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"E{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            // Add events on following days so day score exists
            for (int i = 1; i < 4; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"Follow{i}", Intensity = 4 };
                db.Events.Add(ev);
            }
            await db.SaveChangesAsync();

            var result = await service.GetNextDayEffectsAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(5), TimeSpan.Zero));

            Assert.Single(result);
            Assert.Equal("Exercise", result[0].TagName);
        }

        [Fact]
        public async Task GetNextDayEffectsAsync_TagOn2Days_IsExcluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Exercise", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var dayStart = now.Date;
            for (int i = 0; i < 2; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"E{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetNextDayEffectsAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(5), TimeSpan.Zero));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetNextDayEffectsAsync_OrderedByAbsNextDayScore()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tagA = new Tag { Id = Guid.NewGuid(), Name = "TagA", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            var tagB = new Tag { Id = Guid.NewGuid(), Name = "TagB", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.AddRange(tagA, tagB);

            var dayStart = DateTimeOffset.UtcNow.Date;

            // TagA on days 0, 1, 2 with next day positive (should have high positive next-day score)
            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"A{i}", Intensity = 5 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tagA.Id });
            }

            // TagB on days 3, 4, 5 with next day negative (should have negative next-day score)
            for (int i = 3; i < 6; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"B{i}", Intensity = 7 };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tagB.Id });
            }

            // Add following day events with opposite valence
            for (int i = 1; i < 4; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"FollowPos{i}", Intensity = 8 };
                db.Events.Add(ev);
            }
            for (int i = 4; i < 7; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"FollowNeg{i}", Intensity = 2 };
                db.Events.Add(ev);
            }

            await db.SaveChangesAsync();

            var result = await service.GetNextDayEffectsAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(8), TimeSpan.Zero));

            Assert.Equal(2, result.Count);
            // Order by absolute next-day score (higher absolute value first)
            Assert.True(Math.Abs(result[0].NextDayAvgScore) >= Math.Abs(result[1].NextDayAvgScore));
        }


        #endregion

        #region GetTagCombosAsync

        [Fact]
        public async Task GetTagCombosAsync_TwoTagsOnSameDay3Times_IsIncluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Stress", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.AddRange(tag1, tag2);

            var now = DateTimeOffset.UtcNow;
            var dayStart = now.Date;

            // 3 days with both tags
            for (int i = 0; i < 3; i++)
            {
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"W{i}", Intensity = 5 };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i).AddHours(1), TimeSpan.Zero), Type = EventType.Negative, Title = $"S{i}", Intensity = 5 };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
                db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(4), TimeSpan.Zero));

            Assert.Single(result);
            Assert.Equal(3, result[0].CoOccurrences);
        }

        [Fact]
        public async Task GetTagCombosAsync_CoOccurrences2_IsExcluded()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Stress", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.AddRange(tag1, tag2);

            var dayStart = DateTimeOffset.UtcNow.Date;

            // 2 days with both tags
            for (int i = 0; i < 2; i++)
            {
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"W{i}", Intensity = 5 };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i).AddHours(1), TimeSpan.Zero), Type = EventType.Negative, Title = $"S{i}", Intensity = 5 };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
                db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
            }
            await db.SaveChangesAsync();

            var result = await service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(3), TimeSpan.Zero));

            Assert.Empty(result);
        }


        [Fact]
        public async Task GetTagCombosAsync_PairOrdering_AlphabeticalKey()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tagZ = new Tag { Id = Guid.NewGuid(), Name = "Zebra", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            var tagA = new Tag { Id = Guid.NewGuid(), Name = "Apple", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.AddRange(tagZ, tagA);

            var dayStart = DateTimeOffset.UtcNow.Date;

            // Both tags together 3 days
            for (int i = 0; i < 3; i++)
            {
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"Z{i}", Intensity = 5 };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i).AddHours(1), TimeSpan.Zero), Type = EventType.Negative, Title = $"A{i}", Intensity = 5 };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tagZ.Id });
                db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tagA.Id });
            }

            await db.SaveChangesAsync();

            var result = await service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(4), TimeSpan.Zero));

            Assert.Single(result);
            Assert.Equal("Apple", result[0].Tag1);
            Assert.Equal("Zebra", result[0].Tag2);
        }

        [Fact]
        public async Task GetTagCombosAsync_NoEvents_ReturnsEmpty()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var dayStart = DateTimeOffset.UtcNow.Date;
            var result = await service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(7), TimeSpan.Zero));

            Assert.Empty(result);
        }

        #endregion

        #region GetTagTrendAsync

        [Fact]
        public async Task GetTagTrendAsync_Week_GroupsByMonday()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var monday1 = now.AddDays(-(int)now.DayOfWeek + 1);
            var monday2 = monday1.AddDays(7);

            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday1, Type = EventType.Positive, Title = "E1", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday2, Type = EventType.Positive, Title = "E2", Intensity = 5 };
            db.Events.AddRange(ev1, ev2);
            db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag.Id });
            db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag.Id });
            await db.SaveChangesAsync();

            var result = await service.GetTagTrendAsync(userId, monday1, monday2.AddDays(6), Granularity.Week);

            Assert.Single(result);
            Assert.Equal(2, result[0].Points.Count);
        }

        [Fact]
        public async Task GetTagTrendAsync_Month_GroupsByFirstOfMonth()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
            var feb = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);

            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = jan, Type = EventType.Positive, Title = "E1", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = feb, Type = EventType.Positive, Title = "E2", Intensity = 5 };
            db.Events.AddRange(ev1, ev2);
            db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag.Id });
            db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag.Id });
            await db.SaveChangesAsync();

            var result = await service.GetTagTrendAsync(userId, jan, feb.AddDays(15), Granularity.Month);

            Assert.Single(result);
            Assert.Equal(2, result[0].Points.Count);
        }

        [Fact]
        public async Task GetTagTrendAsync_OnlyIncludesEventsWithTag()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var now = DateTimeOffset.UtcNow;
            var monday = now.AddDays(-(int)now.DayOfWeek + 1);

            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "E1", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(1), Type = EventType.Positive, Title = "E2", Intensity = 5 };
            var ev3 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(2), Type = EventType.Positive, Title = "E3", Intensity = 5 };
            db.Events.AddRange(ev1, ev2, ev3);

            db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag.Id });
            db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag.Id });
            // ev3 is not tagged
            await db.SaveChangesAsync();

            var result = await service.GetTagTrendAsync(userId, monday, monday.AddDays(7), Granularity.Week);

            Assert.Single(result);
            Assert.Equal(2, result[0].Points[0].Count);
        }

        #endregion

        #region GetTrendsAsync

        [Fact]
        public async Task GetTrendsAsync_Week_GroupsByMonday()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var monday1 = now.AddDays(-(int)now.DayOfWeek + 1);
            var monday2 = monday1.AddDays(7);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday1, Type = EventType.Positive, Title = "E1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday2, Type = EventType.Positive, Title = "E2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var result = await service.GetTrendsAsync(userId, monday1, monday2.AddDays(6), Granularity.Week);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetTrendsAsync_Month_GroupsByFirstOfMonth()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
            var feb = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = jan, Type = EventType.Positive, Title = "E1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = feb, Type = EventType.Positive, Title = "E2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var result = await service.GetTrendsAsync(userId, jan, feb.AddDays(15), Granularity.Month);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].PeriodStart.Day);
            Assert.Equal(1, result[1].PeriodStart.Day);
        }

        [Fact]
        public async Task GetTrendsAsync_FiltersOutsideDateRange()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);
            var outside = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);

            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = start.AddDays(5), Type = EventType.Positive, Title = "E1", Intensity = 5 },
                new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = outside, Type = EventType.Positive, Title = "E2", Intensity = 5 }
            );
            await db.SaveChangesAsync();

            var result = await service.GetTrendsAsync(userId, start, end, Granularity.Week);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetTrendsAsync_EmptyRange_ReturnsEmpty()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var result = await service.GetTrendsAsync(userId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), Granularity.Week);

            Assert.Empty(result);
        }

        #endregion

        #region GetInfluenceabilitySplitAsync

        [Fact]
        public async Task GetInfluenceabilitySplitAsync_OnlyNegativeEvents_Considered()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;

            // 3 positive (should be ignored) + 2 negative
            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 8 };
                db.Events.Add(ev);
            }
            for (int i = 0; i < 2; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(1).AddHours(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5, CanInfluence = true };
                db.Events.Add(ev);
            }
            await db.SaveChangesAsync();

            var result = await service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(2));

            Assert.Equal(2, result.CanInfluenceCount + result.CannotInfluenceCount);
        }

        [Fact]
        public async Task GetInfluenceabilitySplitAsync_Split_CalculatedCorrectly()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;

            // 3 influenceable (intensity 6 each)
            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Negative, Title = $"Inf{i}", Intensity = 6, CanInfluence = true };
                db.Events.Add(ev);
            }
            // 2 not influenceable (intensity 4 each)
            for (int i = 0; i < 2; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(10 + i), Type = EventType.Negative, Title = $"NotInf{i}", Intensity = 4, CanInfluence = false };
                db.Events.Add(ev);
            }
            await db.SaveChangesAsync();

            var result = await service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(1));

            Assert.Equal(3, result.CanInfluenceCount);
            Assert.Equal(2, result.CannotInfluenceCount);
            Assert.Equal(6.0, result.CanInfluenceAvgIntensity);
            Assert.Equal(4.0, result.CannotInfluenceAvgIntensity);
        }

        [Fact]
        public async Task GetInfluenceabilitySplitAsync_AllCanInfluence_CannotIsZero()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < 3; i++)
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 6, CanInfluence = true };
                db.Events.Add(ev);
            }
            await db.SaveChangesAsync();

            var result = await service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(1));

            Assert.Equal(3, result.CanInfluenceCount);
            Assert.Equal(0, result.CannotInfluenceCount);
            Assert.Equal(0.0, result.CannotInfluenceAvgIntensity);
        }

        [Fact]
        public async Task GetInfluenceabilitySplitAsync_NoNegativeEvents_AllZeros()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;

            var result = await service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(7));

            Assert.Equal(0, result.CanInfluenceCount);
            Assert.Equal(0, result.CannotInfluenceCount);
            Assert.Equal(0.0, result.CanInfluenceAvgIntensity);
            Assert.Equal(0.0, result.CannotInfluenceAvgIntensity);
        }

        #endregion

        #region GetWeekdayStatsAsync

        [Fact]
        public async Task GetWeekdayStatsAsync_AlwaysReturnsSevenDays()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            // Events only on Monday 2024-04-01 and Wednesday 2024-04-03 (noon UTC — stays same calendar day in Vilnius UTC+3)
            var monday = new DateTimeOffset(2024, 4, 1, 12, 0, 0, TimeSpan.Zero);
            var wednesday = new DateTimeOffset(2024, 4, 3, 12, 0, 0, TimeSpan.Zero);

            db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "Mon", Intensity = 5 });
            db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = wednesday, Type = EventType.Negative, Title = "Wed", Intensity = 3 });
            await db.SaveChangesAsync();

            var result = await service.GetWeekdayStatsAsync(userId, monday, wednesday.AddDays(4));

            Assert.Equal(7, result.Count);
            var tuesday = result.First(d => d.Day == "Tuesday");
            Assert.Equal(0, tuesday.PosCount);
            Assert.Equal(0, tuesday.NegCount);
            Assert.Equal(0.0, tuesday.AvgIntensity);
        }

        [Fact]
        public async Task GetWeekdayStatsAsync_OrderIsMonToSun()
        {
            var db = CreateDbContext();
            var service = new InsightsService(db, CreateConfig());
            var userId = Guid.NewGuid();

            var result = await service.GetWeekdayStatsAsync(userId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

            Assert.Equal(7, result.Count);
            Assert.Equal("Monday", result[0].Day);
            Assert.Equal("Sunday", result[6].Day);
        }

        #endregion
    }
}
