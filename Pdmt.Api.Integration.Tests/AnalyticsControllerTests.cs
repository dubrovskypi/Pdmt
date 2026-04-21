using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Analytics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pdmt.Api.Integration.Tests;

public class AnalyticsControllerTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private static readonly Guid TestUserId = TestAuthHandler.TestUserId;
    private static readonly Guid OtherUserId = Guid.NewGuid();

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/analytics/weekly-summary?weekOf=2025-01-06")]
    [InlineData("/api/analytics/correlations?tagId=00000000-0000-0000-0000-000000000099&from=2025-01-01&to=2025-01-31")]
    [InlineData("/api/analytics/calendar/week?weekOf=2025-01-06")]
    [InlineData("/api/analytics/calendar/month?month=2025-01")]
    public async Task AnalyticsEndpoints_Anonymous_Returns401(string url)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCorrelations_FromAfterTo_Returns400()
    {
        var tagId = Guid.NewGuid();
        var client = CreateTestAuthClient();

        var response = await client.GetAsync(
            $"/api/analytics/correlations?tagId={tagId}&from=2025-06-01&to=2025-01-01", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendarMonth_InvalidFormat_Returns400()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/month?month=2025-1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendarMonth_ValidFormat_Returns200()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/month?month=2025-01", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── WeeklySummary ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWeeklySummary_NoEvents_ReturnsZeroedSummary()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-02-03", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>(TestContext.Current.CancellationToken);

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
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnsureUserExists(db, TestUserId);
            var week = new DateTime(2025, 3, 3, 0, 0, 0, DateTimeKind.Utc); // Monday
            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week, Type = EventType.Positive, Title = "an_p1", Intensity = 7 },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week.AddDays(1), Type = EventType.Positive, Title = "an_p2", Intensity = 8 },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week.AddDays(2), Type = EventType.Negative, Title = "an_n1", Intensity = 5 }
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-03-03", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, result!.PosCount);
        Assert.Equal(1, result.NegCount);
    }

    [Fact]
    public async Task GetWeeklySummary_OtherUsersEvents_NotIncluded()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnsureUserExists(db, OtherUserId);
            var week = new DateTime(2025, 4, 7, 0, 0, 0, DateTimeKind.Utc);
            db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = OtherUserId, Timestamp = week, Type = EventType.Positive, Title = "an_other_p1", Intensity = 9 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-04-07", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>(TestContext.Current.CancellationToken);

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
            $"/api/analytics/correlations?tagId={unknownTagId}&from=2025-01-01&to=2025-01-31", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCorrelations_WithAndWithoutTag_ReturnsSplit()
    {
        Tag tag;
        using (var scope = factory.Services.CreateScope())
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
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = CreateTestAuthClient();
        var response = await client.GetAsync(
            $"/api/analytics/correlations?tagId={tag.Id}&from=2025-05-01&to=2025-05-31", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CorrelationsDto>(TestContext.Current.CancellationToken);

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

        var response = await client.GetAsync("/api/analytics/calendar/week?weekOf=2025-06-02", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CalendarWeekDto>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(7, result.Days.Count);
    }

    // ── CalendarMonth ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCalendarMonth_April_Returns30Days()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/analytics/calendar/month?month=2025-04", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CalendarMonthDto>(TestContext.Current.CancellationToken);

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
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");
        return client;
    }
}
