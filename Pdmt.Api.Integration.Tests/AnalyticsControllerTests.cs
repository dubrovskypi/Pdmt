using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Analytics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pdmt.Api.Integration.Tests;

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
    [InlineData("/api/analytics/weekly-summary?weekOf=2025-01-06")]
    [InlineData("/api/analytics/correlations?tagId=00000000-0000-0000-0000-000000000099&from=2025-01-01&to=2025-01-31")]
    [InlineData("/api/analytics/calendar/week?weekOf=2025-01-06")]
    [InlineData("/api/analytics/calendar/month?month=2025-01")]
    public async Task AnalyticsEndpoints_Anonymous_Returns401(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCorrelations_FromAfterTo_Returns400()
    {
        var tagId = Guid.NewGuid();
        var client = CreateTestAuthClient();

        var response = await client.GetAsync(
            $"/api/analytics/correlations?tagId={tagId}&from=2025-06-01&to=2025-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendarMonth_InvalidFormat_Returns400()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/month?month=2025-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendarMonth_ValidFormat_Returns200()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/month?month=2025-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── WeeklySummary ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWeeklySummary_NoEvents_ReturnsZeroedSummary()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-02-03");
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(0, result.PosCount);
        Assert.Equal(0, result.NegCount);
        Assert.Equal(0.0, result.PosToNegRatio);
        Assert.Empty(result.TopTags);
    }

    [Fact]
    public async Task GetWeeklySummary_WithEvents_ReturnsCorrectCounts()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnsureUserExists(db, TestUserId);
            var week = new DateTime(2025, 3, 3, 0, 0, 0, DateTimeKind.Utc); // Monday
            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week, Type = EventType.Positive, Title = "an_p1", Intensity = 7 },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week.AddDays(1), Type = EventType.Positive, Title = "an_p2", Intensity = 8 },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week.AddDays(2), Type = EventType.Negative, Title = "an_n1", Intensity = 5 }
            );
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-03-03");
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, result!.PosCount);
        Assert.Equal(1, result.NegCount);
    }

    [Fact]
    public async Task GetWeeklySummary_OtherUsersEvents_NotIncluded()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnsureUserExists(db, OtherUserId);
            var week = new DateTime(2025, 4, 7, 0, 0, 0, DateTimeKind.Utc);
            db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = OtherUserId, Timestamp = week, Type = EventType.Positive, Title = "an_other_p1", Intensity = 9 });
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-04-07");
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, result!.PosCount);
    }

    // ── Correlations ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCorrelations_TagNotFound_Returns404()
    {
        var client = CreateTestAuthClient();
        var unknownTagId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/analytics/correlations?tagId={unknownTagId}&from=2025-01-01&to=2025-01-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCorrelations_WithAndWithoutTag_ReturnsSplit()
    {
        Tag tag;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnsureUserExists(db, TestUserId);
            tag = new Tag { Id = Guid.NewGuid(), UserId = TestUserId, Name = "an_corr_tag", CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var baseDate = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var evWithTag = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate, Type = EventType.Positive, Title = "an_corr_with", Intensity = 9 };
            var evWithoutTag = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(1), Type = EventType.Positive, Title = "an_corr_without", Intensity = 4 };
            db.Events.AddRange(evWithTag, evWithoutTag);
            db.EventTags.Add(new EventTag { EventId = evWithTag.Id, TagId = tag.Id });
            await db.SaveChangesAsync();
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync(
            $"/api/analytics/correlations?tagId={tag.Id}&from=2025-05-01&to=2025-05-31");
        var result = await response.Content.ReadFromJsonAsync<CorrelationsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("an_corr_tag", result.TagName);
        Assert.True(result.AvgIntensityWithTag > result.AvgIntensityWithoutTag);
    }

    // ── CalendarWeek ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCalendarWeek_NoEvents_ReturnsSevenDays()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/week?weekOf=2025-06-02");
        var result = await response.Content.ReadFromJsonAsync<CalendarWeekDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(7, result.Days.Count);
    }

    // ── CalendarMonth ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCalendarMonth_April_Returns30Days()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/month?month=2025-04");
        var result = await response.Content.ReadFromJsonAsync<CalendarMonthDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(30, result.Days.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureUserExists(AppDbContext db, Guid userId)
    {
        if (!db.Users.Any(u => u.Id == userId))
            db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
    }

    private HttpClient CreateTestAuthClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");
        return client;
    }
}
