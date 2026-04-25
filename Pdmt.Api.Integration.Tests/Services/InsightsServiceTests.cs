using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Integration.Tests.Infrastructure;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests.Services;

public class InsightsServiceTests : ServiceTestBase
{
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private InsightsService _service = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Db.Users.Add(new UserBuilder().WithId(OtherUserId).WithEmail("other@pdmt.dev").Build());
        await Db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("App:DefaultTimeZone", "Europe/Vilnius")])
            .Build();
        _service = new InsightsService(Db, config);
    }

    #region GetMostIntenseTagsAsync

    [Fact]
    public async Task GetMostIntenseTags_NoEvents_ReturnsEmptyLists()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetMostIntenseTagsAsync(TestAuthHandler.TestUserId, now.AddDays(-7), now);

        result.TopPosTags.Should().BeEmpty();
        result.TopNegTags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMostIntenseTags_WithPositiveTaggedEvents_ReturnsTopByAvgIntensity()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var tagHigh = new Tag { Id = Guid.NewGuid(), Name = "HighTag", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tagLow = new Tag { Id = Guid.NewGuid(), Name = "LowTag", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tagHigh, tagLow);
        var evHigh = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now, Type = EventType.Positive, Title = "H", Intensity = 9 };
        var evLow = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(1), Type = EventType.Positive, Title = "L", Intensity = 3 };
        Db.Events.AddRange(evHigh, evLow);
        Db.EventTags.Add(new EventTag { EventId = evHigh.Id, TagId = tagHigh.Id });
        Db.EventTags.Add(new EventTag { EventId = evLow.Id, TagId = tagLow.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetMostIntenseTagsAsync(userId, now.AddDays(-1), now.AddDays(1));

        result.TopPosTags.Should().HaveCount(2);
        result.TopPosTags[0].TagName.Should().Be("HighTag");
        result.TopPosTags[0].AvgIntensity.Should().Be(9.0);
    }

    [Fact]
    public async Task GetMostIntenseTags_OtherUsersEvents_NotIncluded()
    {
        var now = DateTimeOffset.UtcNow;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "OtherTag", UserId = OtherUserId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var ev = new Event { Id = Guid.NewGuid(), UserId = OtherUserId, Timestamp = now, Type = EventType.Positive, Title = "E", Intensity = 9 };
        Db.Events.Add(ev);
        Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetMostIntenseTagsAsync(TestAuthHandler.TestUserId, now.AddDays(-1), now.AddDays(1));

        result.TopPosTags.Should().BeEmpty();
    }

    #endregion

    #region GetRepeatingTriggersAsync

    [Fact]
    public async Task GetRepeatingTriggersAsync_TagWithExactlyMinCount_IsIncluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

        result.Should().ContainSingle();
        result[0].TagName.Should().Be("Work");
    }

    [Fact]
    public async Task GetRepeatingTriggersAsync_TagBelowMinCount_IsExcluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 2; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepeatingTriggersAsync_OnlyCountsNegativeEvents()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        // 4 positive + 2 negative, but only negative count
        for (int i = 0; i < 4; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        for (int i = 0; i < 2; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepeatingTriggersAsync_OrderedByAvgIntensityDescending()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag1 = new Tag { Id = Guid.NewGuid(), Name = "HighIntensity", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tag2 = new Tag { Id = Guid.NewGuid(), Name = "LowIntensity", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tag1, tag2);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Negative, Title = $"H{i}", Intensity = 9 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(10 + i), Type = EventType.Negative, Title = $"L{i}", Intensity = 3 };
            Db.Events.AddRange(ev1, ev2);
            Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
            Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetRepeatingTriggersAsync(userId, now, now.AddDays(3), minCount: 3);

        result.Should().HaveCount(2);
        result[0].TagName.Should().Be("HighIntensity");
        result[1].TagName.Should().Be("LowIntensity");
    }

    [Fact]
    public async Task GetRepeatingTriggersAsync_NoEvents_ReturnsEmpty()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetRepeatingTriggersAsync(TestAuthHandler.TestUserId, now, now.AddDays(7), minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepeatingTriggersAsync_FiltersOutsideDateRange()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(3);
        for (int i = 0; i < 3; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = start.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        var outsideEv = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = end.AddDays(1), Type = EventType.Negative, Title = "Outside", Intensity = 5 };
        Db.Events.Add(outsideEv);
        Db.EventTags.Add(new EventTag { EventId = outsideEv.Id, TagId = tag.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetRepeatingTriggersAsync(userId, start, end, minCount: 3);

        result.Should().ContainSingle();
        result[0].Count.Should().Be(3);
    }

    [Fact]
    public async Task GetRepeatingTriggersAsync_IsolatesByUserId()
    {
        var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = TestAuthHandler.TestUserId, CreatedAt = DateTimeOffset.UtcNow };
        var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = OtherUserId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tag1, tag2);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var ev1 = new Event { Id = Guid.NewGuid(), UserId = TestAuthHandler.TestUserId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"U1-{i}", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = OtherUserId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"U2-{i}", Intensity = 5 };
            Db.Events.AddRange(ev1, ev2);
            Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
            Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetRepeatingTriggersAsync(TestAuthHandler.TestUserId, now, now.AddDays(7), minCount: 3);

        result.Should().ContainSingle();
        result[0].TagName.Should().Be("Work");
    }

    #endregion

    #region GetBalanceAsync

    [Fact]
    public async Task GetBalance_NoEvents_ReturnsZeroes()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetBalanceAsync(TestAuthHandler.TestUserId, now.AddDays(-7), now);

        result.PosCount.Should().Be(0);
        result.NegCount.Should().Be(0);
        result.AvgPosIntensity.Should().Be(0.0);
        result.AvgNegIntensity.Should().Be(0.0);
    }

    [Fact]
    public async Task GetBalance_MixedEvents_ReturnsCorrectCountsAndAverages()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        Db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now, Type = EventType.Positive, Title = "P1", Intensity = 8 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(1), Type = EventType.Positive, Title = "P2", Intensity = 6 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(2), Type = EventType.Negative, Title = "N1", Intensity = 4 });
        await Db.SaveChangesAsync();

        var result = await _service.GetBalanceAsync(userId, now.AddDays(-1), now.AddDays(1));

        result.PosCount.Should().Be(2);
        result.NegCount.Should().Be(1);
        result.AvgPosIntensity.Should().Be(7.0);
        result.AvgNegIntensity.Should().Be(4.0);
    }

    [Fact]
    public async Task GetBalance_OnlyPositiveEvents_NegAvgIsZero()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now, Type = EventType.Positive, Title = "P1", Intensity = 7 });
        await Db.SaveChangesAsync();

        var result = await _service.GetBalanceAsync(userId, now.AddDays(-1), now.AddDays(1));

        result.PosCount.Should().Be(1);
        result.NegCount.Should().Be(0);
        result.AvgNegIntensity.Should().Be(0.0);
    }

    #endregion

    #region GetDiscountedPositivesAsync

    [Fact]
    public async Task GetDiscountedPositivesAsync_TagWith5EventsAvgBelow4_IsIncluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 3 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetDiscountedPositivesAsync(userId, now, now.AddDays(1));

        result.Should().ContainSingle();
        result[0].TagName.Should().Be("Achievement");
    }

    [Fact]
    public async Task GetDiscountedPositivesAsync_TagWith4Events_IsExcluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 4; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 3 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetDiscountedPositivesAsync(userId, now, now.AddDays(1));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiscountedPositivesAsync_TagWithAvgExactly4_IsExcluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 4 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetDiscountedPositivesAsync(userId, now, now.AddDays(1));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiscountedPositivesAsync_OnlyPositiveEvents_Considered()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Achievement", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        // 3 positive + 5 negative with same tag
        for (int i = 0; i < 3; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 3 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        for (int i = 0; i < 5; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 2 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetDiscountedPositivesAsync(userId, now, now.AddDays(7));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiscountedPositivesAsync_EmptyResult_WhenNoneQualify()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetDiscountedPositivesAsync(TestAuthHandler.TestUserId, now, now.AddDays(7));

        result.Should().BeEmpty();
    }

    #endregion

    #region GetNextDayEffectsAsync

    [Fact]
    public async Task GetNextDayEffectsAsync_TagOnExactly3Days_IsIncluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Exercise", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var dayStart = DateTimeOffset.UtcNow.Date;
        for (int i = 0; i < 3; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"E{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        // Add events on following days so day score exists
        for (int i = 1; i < 4; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"Follow{i}", Intensity = 4 });
        await Db.SaveChangesAsync();

        var result = await _service.GetNextDayEffectsAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(5), TimeSpan.Zero));

        result.Should().ContainSingle();
        result[0].TagName.Should().Be("Exercise");
    }

    [Fact]
    public async Task GetNextDayEffectsAsync_TagOn2Days_IsExcluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Exercise", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var dayStart = DateTimeOffset.UtcNow.Date;
        for (int i = 0; i < 2; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"E{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetNextDayEffectsAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(5), TimeSpan.Zero));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNextDayEffectsAsync_OrderedByAbsNextDayScore()
    {
        var userId = TestAuthHandler.TestUserId;
        var tagA = new Tag { Id = Guid.NewGuid(), Name = "TagA", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tagB = new Tag { Id = Guid.NewGuid(), Name = "TagB", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tagA, tagB);
        var dayStart = DateTimeOffset.UtcNow.Date;
        // TagA on days 0, 1, 2 with next day positive (should have high positive next-day score)
        for (int i = 0; i < 3; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"A{i}", Intensity = 5 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tagA.Id });
        }
        // TagB on days 3, 4, 5 with next day negative (should have negative next-day score)
        for (int i = 3; i < 6; i++)
        {
            var ev = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"B{i}", Intensity = 7 };
            Db.Events.Add(ev);
            Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tagB.Id });
        }
        // Add following day events with opposite valence
        for (int i = 1; i < 4; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Positive, Title = $"FollowPos{i}", Intensity = 8 });
        for (int i = 4; i < 7; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"FollowNeg{i}", Intensity = 2 });
        await Db.SaveChangesAsync();

        var result = await _service.GetNextDayEffectsAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(8), TimeSpan.Zero));

        result.Should().HaveCount(2);
        Math.Abs(result[0].NextDayAvgScore).Should().BeGreaterThanOrEqualTo(Math.Abs(result[1].NextDayAvgScore));
    }

    #endregion

    #region GetTagCombosAsync

    [Fact]
    public async Task GetTagCombosAsync_TwoTagsOnSameDay3Times_IsIncluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Stress", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tag1, tag2);
        var dayStart = DateTimeOffset.UtcNow.Date;
        // 3 days with both tags
        for (int i = 0; i < 3; i++)
        {
            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"W{i}", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i).AddHours(1), TimeSpan.Zero), Type = EventType.Negative, Title = $"S{i}", Intensity = 5 };
            Db.Events.AddRange(ev1, ev2);
            Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
            Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(4), TimeSpan.Zero));

        result.Should().ContainSingle();
        result[0].CoOccurrences.Should().Be(3);
    }

    [Fact]
    public async Task GetTagCombosAsync_CoOccurrences2_IsExcluded()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Stress", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tag1, tag2);
        var dayStart = DateTimeOffset.UtcNow.Date;
        // 2 days with both tags
        for (int i = 0; i < 2; i++)
        {
            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"W{i}", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i).AddHours(1), TimeSpan.Zero), Type = EventType.Negative, Title = $"S{i}", Intensity = 5 };
            Db.Events.AddRange(ev1, ev2);
            Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag1.Id });
            Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag2.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(3), TimeSpan.Zero));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTagCombosAsync_PairOrdering_AlphabeticalKey()
    {
        var userId = TestAuthHandler.TestUserId;
        var tagZ = new Tag { Id = Guid.NewGuid(), Name = "Zebra", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tagA = new Tag { Id = Guid.NewGuid(), Name = "Apple", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.AddRange(tagZ, tagA);
        var dayStart = DateTimeOffset.UtcNow.Date;
        // Both tags together 3 days
        for (int i = 0; i < 3; i++)
        {
            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i), TimeSpan.Zero), Type = EventType.Negative, Title = $"Z{i}", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(dayStart.AddDays(i).AddHours(1), TimeSpan.Zero), Type = EventType.Negative, Title = $"A{i}", Intensity = 5 };
            Db.Events.AddRange(ev1, ev2);
            Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tagZ.Id });
            Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tagA.Id });
        }
        await Db.SaveChangesAsync();

        var result = await _service.GetTagCombosAsync(userId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(4), TimeSpan.Zero));

        result.Should().ContainSingle();
        result[0].Tag1.Should().Be("Apple");
        result[0].Tag2.Should().Be("Zebra");
    }

    [Fact]
    public async Task GetTagCombosAsync_NoEvents_ReturnsEmpty()
    {
        var dayStart = DateTimeOffset.UtcNow.Date;

        var result = await _service.GetTagCombosAsync(TestAuthHandler.TestUserId, new DateTimeOffset(dayStart, TimeSpan.Zero), new DateTimeOffset(dayStart.AddDays(7), TimeSpan.Zero));

        result.Should().BeEmpty();
    }

    #endregion

    #region GetTagTrendAsync

    [Fact]
    public async Task GetTagTrendAsync_Week_GroupsByMonday()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        var monday1 = now.AddDays(-(int)now.DayOfWeek + 1);
        var monday2 = monday1.AddDays(7);
        var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday1, Type = EventType.Positive, Title = "E1", Intensity = 5 };
        var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday2, Type = EventType.Positive, Title = "E2", Intensity = 5 };
        Db.Events.AddRange(ev1, ev2);
        Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag.Id });
        Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetTagTrendAsync(userId, monday1, monday2.AddDays(6), Granularity.Week);

        result.Should().ContainSingle();
        result[0].Points.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTagTrendAsync_Month_GroupsByFirstOfMonth()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = jan, Type = EventType.Positive, Title = "E1", Intensity = 5 };
        var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = feb, Type = EventType.Positive, Title = "E2", Intensity = 5 };
        Db.Events.AddRange(ev1, ev2);
        Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag.Id });
        Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetTagTrendAsync(userId, jan, feb.AddDays(15), Granularity.Month);

        result.Should().ContainSingle();
        result[0].Points.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTagTrendAsync_OnlyIncludesEventsWithTag()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        Db.Tags.Add(tag);
        var now = DateTimeOffset.UtcNow;
        var monday = now.AddDays(-(int)now.DayOfWeek + 1);
        var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "E1", Intensity = 5 };
        var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(1), Type = EventType.Positive, Title = "E2", Intensity = 5 };
        var ev3 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday.AddDays(2), Type = EventType.Positive, Title = "E3", Intensity = 5 };
        Db.Events.AddRange(ev1, ev2, ev3);
        Db.EventTags.Add(new EventTag { EventId = ev1.Id, TagId = tag.Id });
        Db.EventTags.Add(new EventTag { EventId = ev2.Id, TagId = tag.Id });
        // ev3 is not tagged
        await Db.SaveChangesAsync();

        var result = await _service.GetTagTrendAsync(userId, monday, monday.AddDays(7), Granularity.Week);

        result.Should().ContainSingle();
        result[0].Points[0].Count.Should().Be(2);
    }

    #endregion

    #region GetTrendsAsync

    [Fact]
    public async Task GetTrendsAsync_Week_GroupsByMonday()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var monday1 = now.AddDays(-(int)now.DayOfWeek + 1);
        var monday2 = monday1.AddDays(7);
        Db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday1, Type = EventType.Positive, Title = "E1", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday2, Type = EventType.Positive, Title = "E2", Intensity = 5 });
        await Db.SaveChangesAsync();

        var result = await _service.GetTrendsAsync(userId, monday1, monday2.AddDays(6), Granularity.Week);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTrendsAsync_Month_GroupsByFirstOfMonth()
    {
        var userId = TestAuthHandler.TestUserId;
        var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);
        Db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = jan, Type = EventType.Positive, Title = "E1", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = feb, Type = EventType.Positive, Title = "E2", Intensity = 5 });
        await Db.SaveChangesAsync();

        var result = await _service.GetTrendsAsync(userId, jan, feb.AddDays(15), Granularity.Month);

        result.Should().HaveCount(2);
        result[0].PeriodStart.Day.Should().Be(1);
        result[1].PeriodStart.Day.Should().Be(1);
    }

    [Fact]
    public async Task GetTrendsAsync_FiltersOutsideDateRange()
    {
        var userId = TestAuthHandler.TestUserId;
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);
        Db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = start.AddDays(5), Type = EventType.Positive, Title = "E1", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), Type = EventType.Positive, Title = "E2", Intensity = 5 });
        await Db.SaveChangesAsync();

        var result = await _service.GetTrendsAsync(userId, start, end, Granularity.Week);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetTrendsAsync_EmptyRange_ReturnsEmpty()
    {
        var result = await _service.GetTrendsAsync(TestAuthHandler.TestUserId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), Granularity.Week);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetInfluenceabilitySplitAsync

    [Fact]
    public async Task GetInfluenceabilitySplitAsync_OnlyNegativeEvents_Considered()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        // 3 positive (should be ignored) + 2 negative
        for (int i = 0; i < 3; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Positive, Title = $"P{i}", Intensity = 8 });
        for (int i = 0; i < 2; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddDays(1).AddHours(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 5, CanInfluence = true });
        await Db.SaveChangesAsync();

        var result = await _service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(2));

        (result.CanInfluenceCount + result.CannotInfluenceCount).Should().Be(2);
    }

    [Fact]
    public async Task GetInfluenceabilitySplitAsync_Split_CalculatedCorrectly()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        // 3 influenceable (intensity 6 each)
        for (int i = 0; i < 3; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Negative, Title = $"Inf{i}", Intensity = 6, CanInfluence = true });
        // 2 not influenceable (intensity 4 each)
        for (int i = 0; i < 2; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(10 + i), Type = EventType.Negative, Title = $"NotInf{i}", Intensity = 4, CanInfluence = false });
        await Db.SaveChangesAsync();

        var result = await _service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(1));

        result.CanInfluenceCount.Should().Be(3);
        result.CannotInfluenceCount.Should().Be(2);
        result.CanInfluenceAvgIntensity.Should().Be(6.0);
        result.CannotInfluenceAvgIntensity.Should().Be(4.0);
    }

    [Fact]
    public async Task GetInfluenceabilitySplitAsync_AllCanInfluence_CannotIsZero()
    {
        var userId = TestAuthHandler.TestUserId;
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 3; i++)
            Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = now.AddHours(i), Type = EventType.Negative, Title = $"N{i}", Intensity = 6, CanInfluence = true });
        await Db.SaveChangesAsync();

        var result = await _service.GetInfluenceabilitySplitAsync(userId, now, now.AddDays(1));

        result.CanInfluenceCount.Should().Be(3);
        result.CannotInfluenceCount.Should().Be(0);
        result.CannotInfluenceAvgIntensity.Should().Be(0.0);
    }

    [Fact]
    public async Task GetInfluenceabilitySplitAsync_NoNegativeEvents_AllZeros()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await _service.GetInfluenceabilitySplitAsync(TestAuthHandler.TestUserId, now, now.AddDays(7));

        result.CanInfluenceCount.Should().Be(0);
        result.CannotInfluenceCount.Should().Be(0);
        result.CanInfluenceAvgIntensity.Should().Be(0.0);
        result.CannotInfluenceAvgIntensity.Should().Be(0.0);
    }

    #endregion

    #region GetWeekdayStatsAsync

    [Fact]
    public async Task GetWeekdayStatsAsync_AlwaysReturnsSevenDays()
    {
        var userId = TestAuthHandler.TestUserId;
        // Events only on Monday 2024-04-01 and Wednesday 2024-04-03 (noon UTC — stays same calendar day in Vilnius UTC+3)
        var monday = new DateTimeOffset(2024, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var wednesday = new DateTimeOffset(2024, 4, 3, 12, 0, 0, TimeSpan.Zero);
        Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = monday, Type = EventType.Positive, Title = "Mon", Intensity = 5 });
        Db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = wednesday, Type = EventType.Negative, Title = "Wed", Intensity = 3 });
        await Db.SaveChangesAsync();

        var result = await _service.GetWeekdayStatsAsync(userId, monday, wednesday.AddDays(4));

        result.Should().HaveCount(7);
        var tuesday = result.First(d => d.Day == "Tuesday");
        tuesday.PosCount.Should().Be(0);
        tuesday.NegCount.Should().Be(0);
        tuesday.AvgIntensity.Should().Be(0.0);
    }

    [Fact]
    public async Task GetWeekdayStatsAsync_OrderIsMonToSun()
    {
        var result = await _service.GetWeekdayStatsAsync(TestAuthHandler.TestUserId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        result.Should().HaveCount(7);
        result[0].Day.Should().Be("Monday");
        result[6].Day.Should().Be("Sunday");
    }

    #endregion
}
