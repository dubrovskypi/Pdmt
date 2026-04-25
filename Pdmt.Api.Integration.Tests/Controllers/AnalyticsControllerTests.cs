using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Pdmt.Api.Integration.Tests.Controllers;

public class AnalyticsControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private readonly HttpClient _anonClient = factory.CreateClient();

    #region Auth

    [Theory]
    [InlineData("/api/analytics/weekly-summary?weekOf=2025-01-06")]
    [InlineData("/api/analytics/correlations?tagId=00000000-0000-0000-0000-000000000099&from=2025-01-01T00:00:00Z&to=2025-01-31T00:00:00Z")]
    [InlineData("/api/analytics/calendar/week?weekOf=2025-01-06")]
    [InlineData("/api/analytics/calendar/month?month=2025-01")]
    public async Task AnalyticsEndpoints_Anonymous_Returns401(string url)
    {
        var response = await _anonClient.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task GetCorrelations_FromAfterTo_Returns400()
    {
        var tagId = Guid.NewGuid();

        var response = await Client.GetAsync(
            $"/api/analytics/correlations?tagId={tagId}&from=2025-06-01T00:00:00Z&to=2025-01-01T00:00:00Z",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCalendarMonth_InvalidFormat_Returns400()
    {
        var response = await Client.GetAsync("/api/analytics/calendar/month?month=2025-1",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region WeeklySummary

    [Fact]
    public async Task GetWeeklySummary_NoEvents_ReturnsZeroedSummary()
    {
        var response = await Client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-02-03",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.PosCount.Should().Be(0);
        result.NegCount.Should().Be(0);
        result.PosToNegRatio.Should().Be(0.0);
        result.TopTags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeeklySummary_WithEvents_ReturnsCorrectCounts()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var week = new DateTime(2025, 3, 3, 0, 0, 0, DateTimeKind.Utc); // Monday
            db.Events.AddRange(
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week, Type = EventType.Positive, Title = "an_p1", Intensity = 7 },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week.AddDays(1), Type = EventType.Positive, Title = "an_p2", Intensity = 8 },
                new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = week.AddDays(2), Type = EventType.Negative, Title = "an_n1", Intensity = 5 }
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-03-03",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.PosCount.Should().Be(2);
        result.NegCount.Should().Be(1);
    }

    [Fact]
    public async Task GetWeeklySummary_OtherUsersEvents_NotIncluded()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = OtherUserId, Email = $"{OtherUserId}@test.com", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
            var week = new DateTime(2025, 4, 7, 0, 0, 0, DateTimeKind.Utc);
            db.Events.Add(new Event { Id = Guid.NewGuid(), UserId = OtherUserId, Timestamp = week, Type = EventType.Positive, Title = "an_other_p1", Intensity = 9 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/analytics/weekly-summary?weekOf=2025-04-07",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WeeklySummaryDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.PosCount.Should().Be(0);
    }

    #endregion

    #region Correlations

    [Fact]
    public async Task GetCorrelations_TagNotFound_Returns404()
    {
        var unknownTagId = Guid.NewGuid();

        var response = await Client.GetAsync(
            $"/api/analytics/correlations?tagId={unknownTagId}&from=2025-01-01T00:00:00Z&to=2025-01-31T00:00:00Z",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCorrelations_WithAndWithoutTag_ReturnsSplit()
    {
        Tag tag;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tag = new Tag { Id = Guid.NewGuid(), UserId = TestUserId, Name = "an_corr_tag", CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var baseDate = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var evWithTag = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate, Type = EventType.Positive, Title = "an_corr_with", Intensity = 9 };
            var evWithoutTag = new Event { Id = Guid.NewGuid(), UserId = TestUserId, Timestamp = baseDate.AddDays(1), Type = EventType.Positive, Title = "an_corr_without", Intensity = 4 };
            db.Events.AddRange(evWithTag, evWithoutTag);
            db.EventTags.Add(new EventTag { EventId = evWithTag.Id, TagId = tag.Id });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync(
            $"/api/analytics/correlations?tagId={tag.Id}&from=2025-05-01T00:00:00Z&to=2025-05-31T00:00:00Z",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CorrelationsDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.TagName.Should().Be("an_corr_tag");
        result.AvgIntensityWithTag.Should().BeGreaterThan(result.AvgIntensityWithoutTag);
    }

    #endregion

    #region CalendarWeek

    [Fact]
    public async Task GetCalendarWeek_NoEvents_ReturnsSevenDays()
    {
        var response = await Client.GetAsync("/api/analytics/calendar/week?weekOf=2025-06-02",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CalendarWeekDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Days.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetCalendarWeek_EventAtMidnightLithuania_GroupedByLocalDay()
    {
        // 2025-06-02 22:00 UTC = 2025-06-03 01:00 Europe/Vilnius (EEST = UTC+3 in summer)
        // The event must be grouped into June 3, not June 2 — only real PostgreSQL AtTimeZone reproduces this
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Events.Add(new Event
            {
                Id = Guid.NewGuid(), UserId = TestUserId,
                Timestamp = new DateTimeOffset(2025, 6, 2, 22, 0, 0, TimeSpan.Zero),
                Type = EventType.Positive, Intensity = 5, Title = "tz_midnight_test"
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/analytics/calendar/week?weekOf=2025-06-02",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CalendarWeekDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Days.Single(d => d.Date.Day == 3).PosCount.Should().Be(1);
        result.Days.Single(d => d.Date.Day == 2).PosCount.Should().Be(0);
    }

    #endregion

    #region CalendarMonth

    [Fact]
    public async Task GetCalendarMonth_April_Returns30Days()
    {
        var response = await Client.GetAsync("/api/analytics/calendar/month?month=2025-04",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CalendarMonthDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Days.Should().HaveCount(30);
    }

    #endregion
}
