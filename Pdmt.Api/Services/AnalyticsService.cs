using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;

namespace Pdmt.Api.Services;

public class AnalyticsService(AppDbContext db, IConfiguration config) : IAnalyticsService
{
    private TimeZoneInfo GetTz() =>
        TimeZoneInfo.FindSystemTimeZoneById(config["App:DefaultTimeZone"]!);

    public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateOnly weekOf)
    {
        var monday = DateHelper.GetMonday(weekOf);

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= monday && e.Timestamp < monday.AddDays(7))
            .ToListAsync();

        var posEvents = events.Where(e => e.Type == EventType.Positive).ToList();
        var negEvents = events.Where(e => e.Type == EventType.Negative).ToList();

        var posToNegRatio = negEvents.Count == 0 ? 0.0 : (double)posEvents.Count / negEvents.Count;

        var avgPosIntensity = posEvents.Count == 0 ? 0.0 : posEvents.Average(e => (double)e.Intensity);
        var avgNegIntensity = negEvents.Count == 0 ? 0.0 : negEvents.Average(e => (double)e.Intensity);

        var topTags = events
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TagSummaryDto(g.Key, g.Count(), g.Average(x => (double)x.Intensity)))
            .ToList();

        var topPosEvents = posEvents
            .OrderByDescending(e => e.Intensity)
            .Take(5)
            .Select(e => new TopEventDto(e.Title, e.Intensity, e.Timestamp))
            .ToList();

        var topNegEvents = negEvents
            .OrderByDescending(e => e.Intensity)
            .Take(5)
            .Select(e => new TopEventDto(e.Title, e.Intensity, e.Timestamp))
            .ToList();

        var byDayOfWeek = events
            .GroupBy(e => e.Timestamp.DayOfWeek)
            .OrderBy(g => ((int)g.Key + 6) % 7)
            .Select(g => new DayOfWeekBreakdownDto(
                g.Key.ToString(),
                g.Count(e => e.Type == EventType.Positive),
                g.Count(e => e.Type == EventType.Negative),
                g.Average(e => (double)e.Intensity)))
            .ToList();

        return new WeeklySummaryDto(
            posEvents.Count,
            negEvents.Count,
            posToNegRatio,
            avgPosIntensity,
            avgNegIntensity,
            topTags,
            topPosEvents,
            topNegEvents,
            byDayOfWeek);
    }

    public async Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period)
    {
        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .Select(e => new { e.Timestamp, e.Type, e.Intensity })
            .ToListAsync();

        Func<DateTimeOffset, DateTimeOffset> getKey = period == TrendGranularity.Week
            ? date => DateHelper.GetMonday(date)
            : date => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return raw
            .GroupBy(e => getKey(e.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new TrendPeriodDto(
                DateOnly.FromDateTime(g.Key.DateTime),
                g.Count(e => e.Type == EventType.Positive),
                g.Count(e => e.Type == EventType.Negative),
                g.Average(e => (double)e.Intensity)))
            .ToList();
    }

    public async Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId, DateTimeOffset from, DateTimeOffset to)
    {
        var tag = await db.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId)
            ?? throw new NotFoundException("Tag not found.");

        var allEvents = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .Select(e => new
            {
                e.Timestamp,
                e.Intensity,
                HasTag = e.EventTags.Any(et => et.TagId == tagId)
            })
            .ToListAsync();

        var withTagEvents = allEvents.Where(e => e.HasTag).ToList();
        var withoutTagEvents = allEvents.Where(e => !e.HasTag).ToList();

        var avgIntensityWithTag = withTagEvents.Count == 0 ? 0.0 : withTagEvents.Average(e => (double)e.Intensity);
        var avgIntensityWithoutTag = withoutTagEvents.Count == 0 ? 0.0 : withoutTagEvents.Average(e => (double)e.Intensity);

        var daysOfWeek = withTagEvents
            .GroupBy(e => e.Timestamp.DayOfWeek)
            .OrderBy(g => ((int)g.Key + 6) % 7)
            .Select(g => new DayFrequencyDto(g.Key.ToString(), g.Count()))
            .ToList();

        return new CorrelationsDto(tag.Name, avgIntensityWithTag, avgIntensityWithoutTag, daysOfWeek);
    }

    public async Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateOnly weekOf)
    {
        var monday = DateHelper.GetMonday(weekOf);

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= monday && e.Timestamp < monday.AddDays(7))
            .ToListAsync();

        var tz = GetTz();
        var byDay = events.GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz)).ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<CalendarDayDetailsDto>(7);
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            if (!byDay.TryGetValue(DateHelper.ToLocalDate(date, tz), out var dayEvents))
            {
                days.Add(new CalendarDayDetailsDto(date, 0, 0, 0, 0, 0.0, [], []));
                continue;
            }

            var posEvents = dayEvents.Where(e => e.Type == EventType.Positive).ToList();
            var negEvents = dayEvents.Where(e => e.Type == EventType.Negative).ToList();
            var posSum = posEvents.Sum(e => e.Intensity);
            var negSum = negEvents.Sum(e => e.Intensity);
            var total = dayEvents.Count;
            var dayScore = total == 0 ? 0.0 : (double)(posSum - negSum) / total;

            var topPosTags = posEvents
                .SelectMany(e => e.EventTags.Select(et => et.Tag.Name))
                .GroupBy(n => n)
                .OrderByDescending(g => g.Count())
                .Take(2)
                .Select(g => new TagCountDto(g.Key, g.Count()))
                .ToList();

            var topNegTags = negEvents
                .SelectMany(e => e.EventTags.Select(et => et.Tag.Name))
                .GroupBy(n => n)
                .OrderByDescending(g => g.Count())
                .Take(2)
                .Select(g => new TagCountDto(g.Key, g.Count()))
                .ToList();

            days.Add(new CalendarDayDetailsDto(date, posEvents.Count, negEvents.Count,
                posSum, negSum, dayScore, topPosTags, topNegTags));
        }

        return new CalendarWeekDto(monday, monday.AddDays(6), days);
    }

    public async Task<CalendarMonthDto> GetCalendarMonthAsync(Guid userId, int year, int month)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to)
            .Select(e => new { e.Timestamp, e.Type, e.Intensity })
            .ToListAsync();

        var tz = GetTz();
        var byDay = raw.GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz)).ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<CalendarDayLightDto>(DateTime.DaysInMonth(year, month));
        for (var d = 1; d <= DateTime.DaysInMonth(year, month); d++)
        {
            var date = new DateTimeOffset(year, month, d, 0, 0, 0, TimeSpan.Zero);
            if (!byDay.TryGetValue(DateHelper.ToLocalDate(date, tz), out var dayEvents))
            {
                days.Add(new CalendarDayLightDto(date, 0, 0, 0.0));
                continue;
            }

            var posCount = 0;
            var negCount = 0;
            var posSum = 0;
            var negSum = 0;
            foreach (var e in dayEvents)
            {
                switch (e.Type)
                {
                    case EventType.Positive:
                        posCount++; posSum += e.Intensity; break;
                    case EventType.Negative:
                        negCount++; negSum += e.Intensity; break;
                    default:
                        break;
                }
            }
            var total = dayEvents.Count;
            var dayScore = total == 0 ? 0.0 : (double)(posSum - negSum) / total;
            days.Add(new CalendarDayLightDto(date, posCount, negCount, dayScore));
        }

        return new CalendarMonthDto(days);
    }
}
