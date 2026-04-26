using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pdmt.Api.Integration.Tests.Controllers;

public class InsightsControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    private static readonly Guid OtherUserId = Guid.NewGuid();

    #region Auth

    [Theory]
    [InlineData("/api/insights/repeating-triggers?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/discounted-positives?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/next-day-effects?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/tag-combos?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/tag-trend?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/influenceability?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/balance?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/trends?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/most-intense-tags?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/weekday-stats?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    public async Task InsightEndpoints_Unauthenticated_Returns401(string url)
    {
        var anonClient = Factory.CreateClient();

        var response = await anonClient.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/insights/repeating-triggers?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/discounted-positives?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/next-day-effects?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/tag-combos?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/tag-trend?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/influenceability?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/balance?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/trends?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/most-intense-tags?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    [InlineData("/api/insights/weekday-stats?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z")]
    public async Task InsightsEndpoint_Authenticated_Returns200(string url)
    {
        var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData("/api/insights/repeating-triggers?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/discounted-positives?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/next-day-effects?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/tag-combos?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/tag-trend?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/influenceability?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/balance?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/trends?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/most-intense-tags?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("/api/insights/weekday-stats?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task InsightEndpoints_FromAfterTo_Returns400(string url)
    {
        var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region RepeatingTriggers

    [Fact]
    public async Task GetRepeatingTriggers_CountAboveMinCount_IncludesMatchingTags()
    {
        var tag = await SeedTagAsync(TestUserId, "rt_argument");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Negative, intensity: 7, count: 4,
            baseDate: new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));

        var rareTag = await SeedTagAsync(TestUserId, "rt_rare");
        await SeedEventsWithTagAsync(TestUserId, rareTag, type: EventType.Negative, intensity: 5, count: 2,
            baseDate: new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync("/api/insights/repeating-triggers?from=2026-01-10T00:00:00Z&to=2026-01-31T00:00:00Z&minCount=3",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RepeatingTriggerDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Contain(r => r.TagName == "rt_argument");
        result.Should().NotContain(r => r.TagName == "rt_rare");
    }

    [Fact]
    public async Task GetRepeatingTriggers_PositiveEvents_NotIncluded()
    {
        var tag = await SeedTagAsync(TestUserId, "rt_positive_tag");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Positive, intensity: 6, count: 5,
            baseDate: new DateTime(2026, 1, 22, 0, 0, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync("/api/insights/repeating-triggers?from=2026-01-22T00:00:00Z&to=2026-01-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RepeatingTriggerDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotContain(r => r.TagName == "rt_positive_tag");
    }

    #endregion

    #region DiscountedPositives

    [Fact]
    public async Task GetDiscountedPositives_HighFrequencyLowIntensity_IncludesTag()
    {
        var tag = await SeedTagAsync(TestUserId, "dp_coffee");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Positive, intensity: 2, count: 6,
            baseDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var highTag = await SeedTagAsync(TestUserId, "dp_achievement");
        await SeedEventsWithTagAsync(TestUserId, highTag, type: EventType.Positive, intensity: 8, count: 6,
            baseDate: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync("/api/insights/discounted-positives?from=2026-04-01T00:00:00Z&to=2026-04-30T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DiscountedPositiveDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Contain(r => r.TagName == "dp_coffee");
        result.Should().NotContain(r => r.TagName == "dp_achievement");
    }

    [Fact]
    public async Task GetDiscountedPositives_BelowCountThreshold_ExcludesTag()
    {
        var tag = await SeedTagAsync(TestUserId, "dp_rare_low");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Positive, intensity: 2, count: 3,
            baseDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync("/api/insights/discounted-positives?from=2026-05-01T00:00:00Z&to=2026-05-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DiscountedPositiveDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotContain(r => r.TagName == "dp_rare_low");
    }

    #endregion

    #region NextDayEffects

    [Fact]
    public async Task GetNextDayEffects_WithTaggedEvents_ComputesPositiveScore()
    {
        var tag = await SeedTagAsync(TestUserId, "nde_gym");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 3; i++)
            {
                var eventWithTag = new Event
                {
                    Id = Guid.NewGuid(), UserId = TestUserId,
                    Timestamp = new DateTime(2026, 6, i + 1, 10, 0, 0, DateTimeKind.Utc),
                    Type = EventType.Negative, Intensity = 3, Title = $"nde_gym_event_{i}"
                };
                db.Events.Add(eventWithTag);
                db.EventTags.Add(new EventTag { EventId = eventWithTag.Id, TagId = tag.Id });

                var nextDayEvent = new Event
                {
                    Id = Guid.NewGuid(), UserId = TestUserId,
                    Timestamp = new DateTime(2026, 6, i + 2, 10, 0, 0, DateTimeKind.Utc),
                    Type = EventType.Positive, Intensity = 6, Title = $"nde_gym_nextday_{i}"
                };
                db.Events.Add(nextDayEvent);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/next-day-effects?from=2026-06-01T00:00:00Z&to=2026-06-03T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<NextDayEffectDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var gymEffect = result!.FirstOrDefault(r => r.TagName == "nde_gym");
        gymEffect.Should().NotBeNull();
        gymEffect!.NextDayAvgScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetNextDayEffects_FewerThan3Occurrences_ExcludesTag()
    {
        var tag = await SeedTagAsync(TestUserId, "nde_rare");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Negative, intensity: 5, count: 2,
            baseDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync("/api/insights/next-day-effects?from=2026-07-01T00:00:00Z&to=2026-07-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<NextDayEffectDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotContain(r => r.TagName == "nde_rare");
    }

    #endregion

    #region TagCombos

    [Fact]
    public async Task GetTagCombos_CoOccurring3PlusDays_ReturnsPair()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_work");
        var tagB = await SeedTagAsync(TestUserId, "tc_stress");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 3; i++)
            {
                var date = new DateTime(2026, 8, i + 1, 10, 0, 0, DateTimeKind.Utc);
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = 0, Intensity = 6, Title = $"tc_ev1_{i}" };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date.AddHours(2), Type = 0, Intensity = 5, Title = $"tc_ev2_{i}" };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.AddRange(
                    new EventTag { EventId = ev1.Id, TagId = tagA.Id },
                    new EventTag { EventId = ev2.Id, TagId = tagB.Id });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/tag-combos?from=2026-08-01T00:00:00Z&to=2026-08-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Contain(r =>
            (r.Tag1 == "tc_work" && r.Tag2 == "tc_stress") ||
            (r.Tag1 == "tc_stress" && r.Tag2 == "tc_work"));
    }

    [Fact]
    public async Task GetTagCombos_Below3CoOccurrences_ExcludesPair()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_solo_a");
        var tagB = await SeedTagAsync(TestUserId, "tc_solo_b");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 2; i++)
            {
                var date = new DateTime(2026, 9, i + 1, 10, 0, 0, DateTimeKind.Utc);
                var ev1 = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = 0, Intensity = 5, Title = $"tc_solo_ev1_{i}" };
                var ev2 = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date.AddHours(1), Type = 0, Intensity = 5, Title = $"tc_solo_ev2_{i}" };
                db.Events.AddRange(ev1, ev2);
                db.EventTags.AddRange(
                    new EventTag { EventId = ev1.Id, TagId = tagA.Id },
                    new EventTag { EventId = ev2.Id, TagId = tagB.Id });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/tag-combos?from=2026-09-01T00:00:00Z&to=2026-09-30T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotContain(r =>
            (r.Tag1 == "tc_solo_a" || r.Tag2 == "tc_solo_a") &&
            (r.Tag1 == "tc_solo_b" || r.Tag2 == "tc_solo_b"));
    }

    [Fact]
    public async Task GetTagCombos_TagsOnlyCoOccur_ZeroAloneIntensity()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_always_together_a");
        var tagB = await SeedTagAsync(TestUserId, "tc_always_together_b");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 3; i++)
            {
                var date = new DateTime(2026, 2, i + 1, 10, 0, 0, DateTimeKind.Utc);
                var evA = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Positive, Intensity = 8, Title = $"tc_together_{i}" };
                var evB = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date.AddHours(1), Type = EventType.Positive, Intensity = 8, Title = $"tc_together_b_{i}" };
                db.Events.AddRange(evA, evB);
                db.EventTags.Add(new EventTag { EventId = evA.Id, TagId = tagA.Id });
                db.EventTags.Add(new EventTag { EventId = evB.Id, TagId = tagB.Id });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/tag-combos?from=2026-02-01T00:00:00Z&to=2026-02-28T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var combo = result!.FirstOrDefault(r =>
            (r.Tag1 == "tc_always_together_a" && r.Tag2 == "tc_always_together_b") ||
            (r.Tag1 == "tc_always_together_b" && r.Tag2 == "tc_always_together_a"));
        combo.Should().NotBeNull();
        combo!.Tag1AloneAvgScore.Should().Be(0.0);
        combo.Tag2AloneAvgScore.Should().Be(0.0);
        combo.CombinedAvgScore.Should().Be(8.0);
    }

    [Fact]
    public async Task GetTagCombos_WithMixedData_CalculatesAloneIntensitiesCorrectly()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_calc_a");
        var tagB = await SeedTagAsync(TestUserId, "tc_calc_b");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            for (var i = 0; i < 3; i++)
            {
                var date = new DateTime(2026, 3, i + 1, 10, 0, 0, DateTimeKind.Utc);
                var evA = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Positive, Intensity = 8, Title = $"tc_calc_together_{i}" };
                var evB = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date.AddHours(1), Type = EventType.Positive, Intensity = 8, Title = $"tc_calc_together_b_{i}" };
                db.Events.AddRange(evA, evB);
                db.EventTags.Add(new EventTag { EventId = evA.Id, TagId = tagA.Id });
                db.EventTags.Add(new EventTag { EventId = evB.Id, TagId = tagB.Id });
            }

            for (var i = 0; i < 2; i++)
            {
                var date = new DateTime(2026, 3, i + 4, 10, 0, 0, DateTimeKind.Utc);
                var evA = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Positive, Intensity = 4, Title = $"tc_calc_a_alone_{i}" };
                db.Events.Add(evA);
                db.EventTags.Add(new EventTag { EventId = evA.Id, TagId = tagA.Id });
            }

            var evBAlone = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc), Type = EventType.Positive, Intensity = 6, Title = "tc_calc_b_alone" };
            db.Events.Add(evBAlone);
            db.EventTags.Add(new EventTag { EventId = evBAlone.Id, TagId = tagB.Id });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/tag-combos?from=2026-03-01T00:00:00Z&to=2026-03-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var combo = result!.FirstOrDefault(r =>
            (r.Tag1 == "tc_calc_a" && r.Tag2 == "tc_calc_b") ||
            (r.Tag1 == "tc_calc_b" && r.Tag2 == "tc_calc_a"));
        combo.Should().NotBeNull();
        combo!.CoOccurrences.Should().Be(3);
        combo.CombinedAvgScore.Should().Be(8.0);

        if (combo.Tag1 == "tc_calc_a")
        {
            combo.Tag1AloneAvgScore.Should().Be(4.0);
            combo.Tag2AloneAvgScore.Should().Be(6.0);
        }
        else
        {
            combo.Tag1AloneAvgScore.Should().Be(6.0);
            combo.Tag2AloneAvgScore.Should().Be(4.0);
        }
    }

    #endregion

    #region TagTrend

    [Fact]
    public async Task GetTagTrend_MultipleTagsWithDifferentCounts_ReturnsTop3Ordered()
    {
        var tag1 = await SeedTagAsync(TestUserId, "tt_top1_tag");
        var tag2 = await SeedTagAsync(TestUserId, "tt_top2_tag");
        var tag3 = await SeedTagAsync(TestUserId, "tt_top3_tag");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tag1Dates = new[]
            {
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            };
            foreach (var (date, idx) in tag1Dates.Select((d, i) => (d, i)))
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Negative, Intensity = 5, Title = $"tt_t1_{idx}" };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag1.Id });
            }
            var tag2Dates = new[] {
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            };
            foreach (var (date, idx) in tag2Dates.Select((d, i) => (d, i)))
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Negative, Intensity = 5, Title = $"tt_t2_{idx}" };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag2.Id });
            }
            var ev3 = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Type = EventType.Negative, Intensity = 5, Title = "tt_t3_0" };
            db.Events.Add(ev3);
            db.EventTags.Add(new EventTag { EventId = ev3.Id, TagId = tag3.Id });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/tag-trend?from=2026-10-01T00:00:00Z&to=2026-10-14T00:00:00Z&period=Week",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagTrendSeriesDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().HaveCount(3);
        result![0].TagName.Should().Be("tt_top1_tag");
        result[0].Points.Should().HaveCount(2);
        result[1].TagName.Should().Be("tt_top2_tag");
        result[2].TagName.Should().Be("tt_top3_tag");
    }

    #endregion

    #region InfluenceabilitySplit

    [Fact]
    public async Task GetInfluenceabilitySplit_WithMixedNegativeEvents_SplitsByCanInfluence()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 3; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = EventType.Negative, Intensity = 6, CanInfluence = true, Title = $"inf_can_{i}" });
            for (var i = 0; i < 2; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i + 10), Type = EventType.Negative, Intensity = 8, CanInfluence = false, Title = $"inf_cannot_{i}" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/influenceability?from=2026-11-01T00:00:00Z&to=2026-11-30T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<InfluenceabilitySplitDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.CanInfluenceCount.Should().Be(3);
        result.CanInfluenceAvgIntensity.Should().Be(6.0);
        result.CannotInfluenceCount.Should().Be(2);
        result.CannotInfluenceAvgIntensity.Should().Be(8.0);
    }

    [Fact]
    public async Task GetInfluenceabilitySplit_PositiveEventsOnly_ReturnsZeroCounts()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 5; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = EventType.Positive, Intensity = 7, CanInfluence = true, Title = $"inf_pos_{i}" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/influenceability?from=2026-12-01T00:00:00Z&to=2026-12-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<InfluenceabilitySplitDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.CanInfluenceCount.Should().Be(0);
        result.CannotInfluenceCount.Should().Be(0);
    }

    #endregion

    #region Balance

    [Fact]
    public async Task GetBalance_WithMixedEvents_ReturnsCorrectCountsAndAverages()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2027, 2, 1, 10, 0, 0, DateTimeKind.Utc);
            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate, Type = EventType.Positive, Intensity = 8, Title = "bal_pos1" },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(1), Type = EventType.Positive, Intensity = 6, Title = "bal_pos2" },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(2), Type = EventType.Negative, Intensity = 4, Title = "bal_neg1" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/balance?from=2027-02-01T00:00:00Z&to=2027-02-28T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<PosNegBalanceDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.PosCount.Should().Be(2);
        result.NegCount.Should().Be(1);
        result.AvgPosIntensity.Should().Be(7.0);
        result.AvgNegIntensity.Should().Be(4.0);
    }

    #endregion

    #region Trends

    [Fact]
    public async Task GetTrends_EventsAcrossTwoWeeks_ReturnsTwoGroupedPeriods()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // 2027-03-01 and 2027-03-08 are consecutive Mondays
            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc), Type = EventType.Positive, Intensity = 8, Title = "trend_w1_pos" },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2027, 3, 2, 10, 0, 0, DateTimeKind.Utc), Type = EventType.Negative, Intensity = 4, Title = "trend_w1_neg" },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2027, 3, 8, 10, 0, 0, DateTimeKind.Utc), Type = EventType.Positive, Intensity = 6, Title = "trend_w2_pos" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/trends?from=2027-03-01T00:00:00Z&to=2027-03-14T00:00:00Z&period=Week",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TrendPeriodDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().HaveCount(2);
        result![0].PosCount.Should().Be(1);
        result[0].NegCount.Should().Be(1);
        result[1].PosCount.Should().Be(1);
        result[1].NegCount.Should().Be(0);
    }

    #endregion

    #region MostIntenseTags

    [Fact]
    public async Task GetMostIntenseTags_TaggedEventsWithDifferentIntensities_OrderedByAvgIntensity()
    {
        var highTag = await SeedTagAsync(TestUserId, "mit_high");
        var lowTag = await SeedTagAsync(TestUserId, "mit_low");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2027, 4, 1, 10, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 3; i++)
            {
                var evHigh = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = EventType.Positive, Intensity = 9, Title = $"mit_high_{i}" };
                db.Events.Add(evHigh);
                db.EventTags.Add(new EventTag { EventId = evHigh.Id, TagId = highTag.Id });
                var evLow = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i + 3), Type = EventType.Positive, Intensity = 3, Title = $"mit_low_{i}" };
                db.Events.Add(evLow);
                db.EventTags.Add(new EventTag { EventId = evLow.Id, TagId = lowTag.Id });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/most-intense-tags?from=2027-04-01T00:00:00Z&to=2027-04-30T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<MostIntenseTagsDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.TopPosTags.Should().NotBeEmpty();
        result.TopPosTags[0].TagName.Should().Be("mit_high");
        result.TopPosTags[0].AvgIntensity.Should().Be(9.0);
    }

    #endregion

    #region WeekdayStats

    [Fact]
    public async Task GetWeekdayStats_NoEvents_Returns7Days()
    {
        var response = await Client.GetAsync("/api/insights/weekday-stats?from=2027-05-01T00:00:00Z&to=2027-05-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<WeekdayStatDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetWeekdayStats_EventsOnMonday_CountsCorrectly()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // 2027-05-03 is Monday; 10:00 UTC = 13:00 Vilnius (EEST) → still Monday
            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2027, 5, 3, 10, 0, 0, DateTimeKind.Utc), Type = EventType.Positive, Intensity = 8, Title = "wd_mon_pos" },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2027, 5, 3, 14, 0, 0, DateTimeKind.Utc), Type = EventType.Negative, Intensity = 4, Title = "wd_mon_neg" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/insights/weekday-stats?from=2027-05-01T00:00:00Z&to=2027-05-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<WeekdayStatDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var monday = result!.Single(d => d.Day == "Monday");
        monday.PosCount.Should().Be(1);
        monday.NegCount.Should().Be(1);
        monday.AvgIntensity.Should().Be(6.0);
    }

    #endregion

    #region UserIsolation

    [Theory]
    [InlineData("/api/insights/balance?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "balance")]
    [InlineData("/api/insights/trends?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "list")]
    [InlineData("/api/insights/most-intense-tags?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "most-intense-tags")]
    [InlineData("/api/insights/weekday-stats?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "weekday-stats")]
    [InlineData("/api/insights/repeating-triggers?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "list")]
    [InlineData("/api/insights/discounted-positives?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "list")]
    [InlineData("/api/insights/next-day-effects?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "list")]
    [InlineData("/api/insights/tag-combos?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "list")]
    [InlineData("/api/insights/tag-trend?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "list")]
    [InlineData("/api/insights/influenceability?from=2027-01-01T00:00:00Z&to=2027-01-31T00:00:00Z", "influenceability")]
    public async Task InsightEndpoints_OtherUsersDataOnly_ReturnsCurrentUserEmptyResult(string url, string kind)
    {
        var tag = await SeedTagAsync(OtherUserId, "isolation_other_tag");
        await SeedEventsWithTagAsync(OtherUserId, tag, EventType.Negative, 7, 5,
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertOwnUserEmptyAsync(response, kind);
    }

    #endregion

    #region Helpers

    private static async Task AssertOwnUserEmptyAsync(HttpResponseMessage response, string kind)
    {
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        switch (kind)
        {
            case "list":
                root.GetArrayLength().Should().Be(0);
                break;
            case "balance":
                root.GetProperty("posCount").GetInt32().Should().Be(0);
                root.GetProperty("negCount").GetInt32().Should().Be(0);
                break;
            case "most-intense-tags":
                root.GetProperty("topPosTags").GetArrayLength().Should().Be(0);
                root.GetProperty("topNegTags").GetArrayLength().Should().Be(0);
                break;
            case "weekday-stats":
                root.GetArrayLength().Should().Be(7);
                foreach (var day in root.EnumerateArray())
                {
                    day.GetProperty("posCount").GetInt32().Should().Be(0);
                    day.GetProperty("negCount").GetInt32().Should().Be(0);
                }
                break;
            case "influenceability":
                root.GetProperty("canInfluenceCount").GetInt32().Should().Be(0);
                root.GetProperty("cannotInfluenceCount").GetInt32().Should().Be(0);
                break;
        }
    }

    private async Task<Tag> SeedTagAsync(Guid userId, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Tags.FirstOrDefaultAsync(t => t.UserId == userId && t.Name == name);
        if (existing is not null) return existing;

        var tag = new Tag { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(tag);

        if (!await db.Users.AnyAsync(u => u.Id == userId))
            db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return tag;
    }

    private async Task SeedEventsWithTagAsync(Guid userId, Tag tag, EventType type, int intensity, int count, DateTime baseDate)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < count; i++)
        {
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = baseDate.AddDays(i),
                Type = type,
                Intensity = intensity,
                Title = $"{tag.Name}_event_{i}"
            };
            db.Events.Add(ev);
            db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
