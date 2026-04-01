using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Analytics;
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
    [InlineData("/api/analytics/insights/tag-trend?tagId=00000000-0000-0000-0000-000000000099&from=2026-01-01&to=2026-01-31")]
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
        await SeedEventsWithTagAsync(TestUserId, tag, type: 0, intensity: 7, count: 4,
            baseDate: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var rareTag = await SeedTagAsync(TestUserId, "rt_rare");
        await SeedEventsWithTagAsync(TestUserId, rareTag, type: 0, intensity: 5, count: 2,
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
        await SeedEventsWithTagAsync(TestUserId, tag, type: 1, intensity: 6, count: 5,
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
        await SeedEventsWithTagAsync(TestUserId, tag, type: 1, intensity: 2, count: 6,
            baseDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var highTag = await SeedTagAsync(TestUserId, "dp_achievement");
        await SeedEventsWithTagAsync(TestUserId, highTag, type: 1, intensity: 8, count: 6,
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
        await SeedEventsWithTagAsync(TestUserId, tag, type: 1, intensity: 2, count: 3,
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
                    Type = 0, Intensity = 3, Title = $"nde_gym_event_{i}"
                };
                db.Events.Add(eventWithTag);
                db.EventTags.Add(new EventTag { EventId = eventWithTag.Id, TagId = tag.Id });

                // Next day: 1 positive event
                var nextDayEvent = new Event
                {
                    Id = Guid.NewGuid(), UserId = TestUserId,
                    Timestamp = new DateTime(2026, 6, i + 2, 10, 0, 0, DateTimeKind.Utc),
                    Type = 1, Intensity = 6, Title = $"nde_gym_nextday_{i}"
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
        await SeedEventsWithTagAsync(TestUserId, tag, type: 0, intensity: 5, count: 2,
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

    // ── TagTrend ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagTrend_Should_Return_404_For_Nonexistent_Tag()
    {
        var client = CreateTestAuthClient();
        var nonExistentTagId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/analytics/insights/tag-trend?tagId={nonExistentTagId}&from=2026-01-01&to=2026-01-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTagTrend_Should_Return_404_For_Other_Users_Tag()
    {
        var otherTag = await SeedTagAsync(OtherUserId, "tt_other_user_tag");
        var client = CreateTestAuthClient();

        var response = await client.GetAsync($"/api/analytics/insights/tag-trend?tagId={otherTag.Id}&from=2026-01-01&to=2026-01-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTagTrend_Should_Group_Events_By_Week()
    {
        var tag = await SeedTagAsync(TestUserId, "tt_weekly_tag");
        // Week 1: 2 events, Week 2: 3 events
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dates = new[]
            {
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), // Week 1
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), // Week 1
                new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc), // Week 2
                new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc), // Week 2
                new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc) // Week 2
            };
            foreach (var (date, idx) in dates.Select((d, i) => (d, i)))
            {
                var ev = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = date, Type = 0, Intensity = 5, Title = $"tt_ev_{idx}" };
                db.Events.Add(ev);
                db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
            }
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync($"/api/analytics/insights/tag-trend?tagId={tag.Id}&from=2026-10-01&to=2026-10-14&period=Week");
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TagTrendPointDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, result!.Count);
        Assert.Equal(2, result[0].Count);
        Assert.Equal(3, result[1].Count);
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
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = 0, Intensity = 6, CanInfluence = true, Title = $"inf_can_{i}" });
            for (var i = 0; i < 2; i++)
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i + 10), Type = 0, Intensity = 8, CanInfluence = false, Title = $"inf_cannot_{i}" });
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
                db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(i), Type = 1, Intensity = 7, CanInfluence = true, Title = $"inf_pos_{i}" });
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
        await SeedEventsWithTagAsync(OtherUserId, otherTag, type: 0, intensity: 8, count: 5,
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

    private async Task SeedEventsWithTagAsync(Guid userId, Tag tag, int type, int intensity, int count, DateTime baseDate)
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
