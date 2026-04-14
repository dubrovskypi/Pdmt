using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Insights;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pdmt.Api.Tests;

public class AnalyticsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private readonly CustomWebAppFactory _factory;

    public AnalyticsControllerTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/analytics/insights/repeating-triggers?from=2026-01-01&to=2026-01-31")]
    [InlineData("/api/analytics/insights/discounted-positives?from=2026-01-01&to=2026-01-31")]
    [InlineData("/api/analytics/insights/next-day-effects?from=2026-01-01&to=2026-01-31")]
    [InlineData("/api/analytics/insights/tag-combos?from=2026-01-01&to=2026-01-31")]
    [InlineData("/api/analytics/insights/tag-trend?from=2026-01-01&to=2026-01-31")]
    [InlineData("/api/analytics/insights/influenceability?from=2026-01-01&to=2026-01-31")]
    public async Task InsightEndpoints_Should_Return_401_For_Anonymous(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/analytics/insights/repeating-triggers?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/analytics/insights/discounted-positives?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/analytics/insights/next-day-effects?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/analytics/insights/tag-combos?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/analytics/insights/influenceability?from=2026-02-01&to=2026-01-01")]
    public async Task InsightEndpoints_Should_Return_400_When_From_After_To(string url)
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── RepeatingTriggers ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetRepeatingTriggers_Should_Return_Tags_Meeting_MinCount()
    {
        var tag = await SeedTagAsync(TestUserId, "rt_argument");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Negative, intensity: 7, count: 4,
            baseDate: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var rareTag = await SeedTagAsync(TestUserId, "rt_rare");
        await SeedEventsWithTagAsync(TestUserId, rareTag, type: EventType.Negative, intensity: 5, count: 2,
            baseDate: new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/repeating-triggers?from=2026-02-01&to=2026-02-28&minCount=3");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RepeatingTriggerDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(result!, r => r.TagName == "rt_argument");
        Assert.DoesNotContain(result!, r => r.TagName == "rt_rare");
    }

    [Fact]
    public async Task GetRepeatingTriggers_Should_Only_Consider_Negative_Events()
    {
        var tag = await SeedTagAsync(TestUserId, "rt_positive_tag");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Positive, intensity: 6, count: 5,
            baseDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/repeating-triggers?from=2026-03-01&to=2026-03-31");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RepeatingTriggerDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(result!, r => r.TagName == "rt_positive_tag");
    }

    // ── DiscountedPositives ───────────────────────────────────────────────────

    [Fact]
    public async Task GetDiscountedPositives_Should_Return_HighFrequency_LowIntensity_Tags()
    {
        var tag = await SeedTagAsync(TestUserId, "dp_coffee");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Positive, intensity: 2, count: 6,
            baseDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var highTag = await SeedTagAsync(TestUserId, "dp_achievement");
        await SeedEventsWithTagAsync(TestUserId, highTag, type: EventType.Positive, intensity: 8, count: 6,
            baseDate: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/discounted-positives?from=2026-04-01&to=2026-04-30");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DiscountedPositiveDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(result!, r => r.TagName == "dp_coffee");
        Assert.DoesNotContain(result!, r => r.TagName == "dp_achievement");
    }

    [Fact]
    public async Task GetDiscountedPositives_Should_Exclude_Tags_Below_Count_Threshold()
    {
        var tag = await SeedTagAsync(TestUserId, "dp_rare_low");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Positive, intensity: 2, count: 3,
            baseDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/discounted-positives?from=2026-05-01&to=2026-05-31");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DiscountedPositiveDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(result!, r => r.TagName == "dp_rare_low");
    }

    // ── NextDayEffects ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNextDayEffects_Should_Compute_Correct_NextDay_Score()
    {
        var tag = await SeedTagAsync(TestUserId, "nde_gym");
        // Tag appears on day 1, 2, 3 (3 occurrences)
        // Day 2, 3, 4 — positive events (nextDay score positive)
        using (var scope = _factory.Services.CreateScope())
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

                // Next day: 1 positive event
                var nextDayEvent = new Event
                {
                    Id = Guid.NewGuid(), UserId = TestUserId,
                    Timestamp = new DateTime(2026, 6, i + 2, 10, 0, 0, DateTimeKind.Utc),
                    Type = EventType.Positive, Intensity = 6, Title = $"nde_gym_nextday_{i}"
                };
                db.Events.Add(nextDayEvent);
            }
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/next-day-effects?from=2026-06-01&to=2026-06-03");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<NextDayEffectDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var gymEffect = result!.FirstOrDefault(r => r.TagName == "nde_gym");
        Assert.NotNull(gymEffect);
        Assert.True(gymEffect.NextDayAvgScore > 0);
    }

    [Fact]
    public async Task GetNextDayEffects_Should_Exclude_Tags_With_Fewer_Than_3_Occurrences()
    {
        var tag = await SeedTagAsync(TestUserId, "nde_rare");
        await SeedEventsWithTagAsync(TestUserId, tag, type: EventType.Negative, intensity: 5, count: 2,
            baseDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/next-day-effects?from=2026-07-01&to=2026-07-31");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<NextDayEffectDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(result!, r => r.TagName == "nde_rare");
    }

    // ── TagCombos ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagCombos_Should_Return_Pairs_CoOccurring_3_Plus_Days()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_work");
        var tagB = await SeedTagAsync(TestUserId, "tc_stress");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // TagA + TagB together on 3 different days
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
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/tag-combos?from=2026-08-01&to=2026-08-31");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(result!, r =>
            (r.Tag1 == "tc_work" && r.Tag2 == "tc_stress") ||
            (r.Tag1 == "tc_stress" && r.Tag2 == "tc_work"));
    }

    [Fact]
    public async Task GetTagCombos_Should_Not_Return_Pairs_Below_3_CoOccurrences()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_solo_a");
        var tagB = await SeedTagAsync(TestUserId, "tc_solo_b");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Only 2 days together
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
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/tag-combos?from=2026-09-01&to=2026-09-30");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(result!, r =>
            (r.Tag1 == "tc_solo_a" || r.Tag2 == "tc_solo_a") &&
            (r.Tag1 == "tc_solo_b" || r.Tag2 == "tc_solo_b"));
    }

    [Fact]
    public async Task GetTagCombos_Should_Return_Zero_Alone_Intensities_When_Tags_Only_CoOccur()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_always_together_a");
        var tagB = await SeedTagAsync(TestUserId, "tc_always_together_b");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Days 1-3: TagA + TagB together, never separately (use February to avoid conflicts with other tests)
            for (var i = 0; i < 3; i++)
            {
                var date = new DateTime(2026, 2, i + 1, 10, 0, 0, DateTimeKind.Utc);
                var evA = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Positive, Intensity = 8, Title = $"tc_together_{i}" };
                var evB = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date.AddHours(1), Type = EventType.Positive, Intensity = 8, Title = $"tc_together_b_{i}" };
                db.Events.AddRange(evA, evB);
                db.EventTags.Add(new EventTag { EventId = evA.Id, TagId = tagA.Id });
                db.EventTags.Add(new EventTag { EventId = evB.Id, TagId = tagB.Id });
            }

            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/tag-combos?from=2026-02-01&to=2026-02-28");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var combo = result!.FirstOrDefault(r =>
            (r.Tag1 == "tc_always_together_a" && r.Tag2 == "tc_always_together_b") ||
            (r.Tag1 == "tc_always_together_b" && r.Tag2 == "tc_always_together_a"));
        Assert.NotNull(combo);

        // Both alone intensities should be 0, since they never appear separately
        Assert.Equal(0.0, combo.Tag1AloneAvgScore);
        Assert.Equal(0.0, combo.Tag2AloneAvgScore);
        Assert.Equal(8.0, combo.CombinedAvgScore);
    }

    [Fact]
    public async Task GetTagCombos_Should_Calculate_Alone_Intensities_Correctly()
    {
        var tagA = await SeedTagAsync(TestUserId, "tc_calc_a");
        var tagB = await SeedTagAsync(TestUserId, "tc_calc_b");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Days 1-3: TagA + TagB together (intensity 8) — use March to avoid conflicts
            for (var i = 0; i < 3; i++)
            {
                var date = new DateTime(2026, 3, i + 1, 10, 0, 0, DateTimeKind.Utc);
                var evA = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Positive, Intensity = 8, Title = $"tc_calc_together_{i}" };
                var evB = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date.AddHours(1), Type = EventType.Positive, Intensity = 8, Title = $"tc_calc_together_b_{i}" };
                db.Events.AddRange(evA, evB);
                db.EventTags.Add(new EventTag { EventId = evA.Id, TagId = tagA.Id });
                db.EventTags.Add(new EventTag { EventId = evB.Id, TagId = tagB.Id });
            }

            // Days 4-5: TagA alone (intensity 4)
            for (var i = 0; i < 2; i++)
            {
                var date = new DateTime(2026, 3, i + 4, 10, 0, 0, DateTimeKind.Utc);
                var evA = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = EventType.Positive, Intensity = 4, Title = $"tc_calc_a_alone_{i}" };
                db.Events.Add(evA);
                db.EventTags.Add(new EventTag { EventId = evA.Id, TagId = tagA.Id });
            }

            // Day 6: TagB alone (intensity 6)
            var evBAlone = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc), Type = EventType.Positive, Intensity = 6, Title = "tc_calc_b_alone" };
            db.Events.Add(evBAlone);
            db.EventTags.Add(new EventTag { EventId = evBAlone.Id, TagId = tagB.Id });

            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/tag-combos?from=2026-03-01&to=2026-03-31");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagComboDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var combo = result!.FirstOrDefault(r =>
            (r.Tag1 == "tc_calc_a" && r.Tag2 == "tc_calc_b") ||
            (r.Tag1 == "tc_calc_b" && r.Tag2 == "tc_calc_a"));
        Assert.NotNull(combo);
        Assert.Equal(3, combo.CoOccurrences);

        // Combined days: dayScore = (8+8)/2 = 8.0 per day, avg = 8.0
        Assert.Equal(8.0, combo.CombinedAvgScore);

        // Tag1 alone (intensity 4, positive only → dayScore 4.0)
        // Tag2 alone (intensity 6, positive only → dayScore 6.0)
        // Order can vary, so check both possibilities
        if (combo.Tag1 == "tc_calc_a")
        {
            Assert.Equal(4.0, combo.Tag1AloneAvgScore);
            Assert.Equal(6.0, combo.Tag2AloneAvgScore);
        }
        else
        {
            Assert.Equal(6.0, combo.Tag1AloneAvgScore);
            Assert.Equal(4.0, combo.Tag2AloneAvgScore);
        }
    }

    // ── TagTrend ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagTrend_Should_Return_Top3_Tags_Ordered_By_Count()
    {
        var tag1 = await SeedTagAsync(TestUserId, "tt_top1_tag");
        var tag2 = await SeedTagAsync(TestUserId, "tt_top2_tag");
        var tag3 = await SeedTagAsync(TestUserId, "tt_top3_tag");
        // tag1: 3 events, tag2: 2 events, tag3: 1 event
        using (var scope = _factory.Services.CreateScope())
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
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/tag-trend?from=2026-10-01&to=2026-10-14&period=Week");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagTrendSeriesDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, result!.Count);
        Assert.Equal("tt_top1_tag", result[0].TagName);
        Assert.Equal(2, result[0].Points.Count); // 2 weeks
        Assert.Equal("tt_top2_tag", result[1].TagName);
        Assert.Equal("tt_top3_tag", result[2].TagName);
    }

    // ── InfluenceabilitySplit ─────────────────────────────────────────────────

    [Fact]
    public async Task GetInfluenceabilitySplit_Should_Split_Negative_Events_By_CanInfluence()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 3; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = EventType.Negative, Intensity = 6, CanInfluence = true, Title = $"inf_can_{i}" });
            for (var i = 0; i < 2; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i + 10), Type = EventType.Negative, Intensity = 8, CanInfluence = false, Title = $"inf_cannot_{i}" });
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/influenceability?from=2026-11-01&to=2026-11-30");
        var result = await response.Content.ReadFromJsonAsync<InfluenceabilitySplitDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(3, result.CanInfluenceCount);
        Assert.Equal(6.0, result.CanInfluenceAvgIntensity);
        Assert.Equal(2, result.CannotInfluenceCount);
        Assert.Equal(8.0, result.CannotInfluenceAvgIntensity);
    }

    [Fact]
    public async Task GetInfluenceabilitySplit_Should_Ignore_Positive_Events()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            // Only positive events — should return zeros
            for (var i = 0; i < 5; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = EventType.Positive, Intensity = 7, CanInfluence = true, Title = $"inf_pos_{i}" });
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/influenceability?from=2026-12-01&to=2026-12-31");
        var result = await response.Content.ReadFromJsonAsync<InfluenceabilitySplitDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(0, result.CanInfluenceCount);
        Assert.Equal(0, result.CannotInfluenceCount);
    }

    // ── User Isolation ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRepeatingTriggers_Should_Not_Return_Other_Users_Data()
    {
        var otherTag = await SeedTagAsync(OtherUserId, "rt_isolation_other");
        await SeedEventsWithTagAsync(OtherUserId, otherTag, type: EventType.Negative, intensity: 8, count: 5,
            baseDate: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/insights/repeating-triggers?from=2026-01-01&to=2026-01-31");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RepeatingTriggerDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(result!, r => r.TagName == "rt_isolation_other");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Tag> SeedTagAsync(Guid userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = db.Tags.FirstOrDefault(t => t.UserId == userId && t.Name == name);
        if (existing is not null) return existing;

        var tag = new Tag { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = DateTime.UtcNow };
        db.Tags.Add(tag);

        // Ensure user exists in DB
        if (!db.Users.Any(u => u.Id == userId))
            db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });

        await db.SaveChangesAsync();
        return tag;
    }

    private async Task SeedEventsWithTagAsync(Guid userId, Tag tag, EventType type, int intensity, int count, DateTime baseDate)
    {
        using var scope = _factory.Services.CreateScope();
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

        await db.SaveChangesAsync();
    }

    private HttpClient CreateTestAuthClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");
        return client;
    }
}
