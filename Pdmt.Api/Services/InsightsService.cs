using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Infrastructure;

namespace Pdmt.Api.Services;

public class InsightsService(AppDbContext db, IConfiguration config) : IInsightsService
{
    private TimeZoneInfo GetTz() =>
        TimeZoneInfo.FindSystemTimeZoneById(config["App:DefaultTimeZone"]!);

    public async Task<MostIntenseTagsDto> GetMostIntenseTagsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeMostIntenseTags(events);
    }

    internal static MostIntenseTagsDto ComputeMostIntenseTags(IReadOnlyList<Event> events)
    {
        var pos = events.Where(e => e.Type == EventType.Positive).ToList();
        var neg = events.Where(e => e.Type == EventType.Negative).ToList();

        var posTags = pos
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .OrderByDescending(g => g.Average(x => (double)x.Intensity))
            .Take(5)
            .Select(g => new TagSummaryDto(g.Key, g.Count(), g.Average(x => (double)x.Intensity)))
            .ToList();
        var negTags = neg
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .OrderByDescending(g => g.Average(x => (double)x.Intensity))
            .Take(5)
            .Select(g => new TagSummaryDto(g.Key, g.Count(), g.Average(x => (double)x.Intensity)))
            .ToList();
        return new MostIntenseTagsDto(posTags, negTags);
    }

    public async Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, int minCount = 3)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeRepeatingTriggers(events, minCount);
    }

    internal static IReadOnlyList<RepeatingTriggerDto> ComputeRepeatingTriggers(IReadOnlyList<Event> events, int minCount)
    {
        return events
            .Where(e => e.Type == EventType.Negative)
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() >= minCount)
            .OrderByDescending(g => g.Average(x => (double)x.Intensity))
            .Select(g => new RepeatingTriggerDto(g.Key, g.Count(), g.Average(x => (double)x.Intensity)))
            .ToList();
    }

    public async Task<PosNegBalanceDto> GetBalanceAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeBalance(events);
    }

    internal static PosNegBalanceDto ComputeBalance(IReadOnlyList<Event> events)
    {
        var pos = events.Where(e => e.Type == EventType.Positive).ToList();
        var neg = events.Where(e => e.Type == EventType.Negative).ToList();

        return new PosNegBalanceDto(
            pos.Count,
            neg.Count,
            pos.Count == 0 ? 0.0 : pos.Average(e => (double)e.Intensity),
            neg.Count == 0 ? 0.0 : neg.Average(e => (double)e.Intensity));
    }

    public async Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, Granularity period)
    {
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeTrends(events, period, GetTz());
    }

    internal static IReadOnlyList<TrendPeriodDto> ComputeTrends(IReadOnlyList<Event> events, Granularity period, TimeZoneInfo tz)
    {
        Func<DateTimeOffset, DateTimeOffset> getKey = period == Granularity.Week
            ? date => DateHelper.GetMonday(date, tz)
            : date => DateHelper.GetFirstDayOfMonth(date, tz);

        return events
            .GroupBy(e => getKey(e.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new TrendPeriodDto(
                DateOnly.FromDateTime(g.Key.DateTime),
                g.Count(e => e.Type == EventType.Positive),
                g.Count(e => e.Type == EventType.Negative),
                g.Average(e => (double)e.Intensity)))
            .ToList();
    }

    public async Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeDiscountedPositives(events);
    }

    internal static IReadOnlyList<DiscountedPositiveDto> ComputeDiscountedPositives(IReadOnlyList<Event> events)
    {
        return events
            .Where(e => e.Type == EventType.Positive)
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() >= 5 && g.Average(x => (double)x.Intensity) < 4.0)
            .OrderByDescending(g => g.Count())
            .Select(g => new DiscountedPositiveDto(g.Key, g.Average(x => (double)x.Intensity), g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<WeekdayStatDto>> GetWeekdayStatsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeWeekdayStats(events, GetTz());
    }

    internal static IReadOnlyList<WeekdayStatDto> ComputeWeekdayStats(IReadOnlyList<Event> events, TimeZoneInfo tz)
    {
        var grouped = events
            .GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz).DayOfWeek)
            .ToDictionary(g => g.Key, g => g.ToList());

        return Enum.GetValues<DayOfWeek>()
            .OrderBy(d => ((int)d + 6) % 7)
            .Select(dow => grouped.TryGetValue(dow, out var g)
                ? new WeekdayStatDto(
                    dow.ToString(),
                    g.Count(e => e.Type == EventType.Positive),
                    g.Count(e => e.Type == EventType.Negative),
                    g.Average(e => (double)e.Intensity))
                : new WeekdayStatDto(dow.ToString(), 0, 0, 0.0))
            .ToList();
    }

    public async Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        // Запрашиваем на 2 дня шире — чтобы вычислить dayScore следующего дня после последнего дня периода
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(2))
            .ToListAsync();

        return ComputeNextDayEffects(events, to, GetTz());
    }

    internal static IReadOnlyList<NextDayEffectDto> ComputeNextDayEffects(IReadOnlyList<Event> events, DateTimeOffset to, TimeZoneInfo tz)
    {
        var dayScores = events
            .GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz))
            .ToDictionary(
                g => g.Key,
                g => (double)(g.Sum(e => e.Type == EventType.Positive ? e.Intensity : 0) - g.Sum(e => e.Type == EventType.Negative ? e.Intensity : 0)) / g.Count());

        var tagDates = events
            .Where(e => e.Timestamp < to.AddDays(1))
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, Date = DateHelper.ToLocalDate(e.Timestamp, tz) }))
            .GroupBy(x => x.Name)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Date).Distinct().ToList());

        return tagDates
            .Where(kv => kv.Value.Count >= 3)
            .Select(kv =>
            {
                var nextDayScores = kv.Value
                    .Select(d => d.AddDays(1))
                    .Where(d => dayScores.ContainsKey(d))
                    .Select(d => dayScores[d])
                    .ToList();

                return nextDayScores.Count == 0
                    ? null
                    : new NextDayEffectDto(kv.Key, nextDayScores.Average(), nextDayScores.Count);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => Math.Abs(x.NextDayAvgScore))
            .ToList();
    }

    public async Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeTagCombos(events, GetTz());
    }

    internal static IReadOnlyList<TagComboDto> ComputeTagCombos(IReadOnlyList<Event> events, TimeZoneInfo tz)
    {
        var byDay = events
            .GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz))
            .Select(g => new
            {
                Tags = g.SelectMany(e => e.EventTags.Select(et => et.Tag.Name)).Distinct().ToList(),
                DayScore = (double)(g.Sum(e => e.Type == EventType.Positive ? e.Intensity : 0)
                                  - g.Sum(e => e.Type == EventType.Negative ? e.Intensity : 0)) / g.Count()
            })
            .ToList();

        var comboData = new Dictionary<(string, string), (List<double> CombinedDayScores, List<double> Tag1AloneDayScores, List<double> Tag2AloneDayScores)>();

        foreach (var day in byDay)
        {
            var tags = day.Tags;
            for (var i = 0; i < tags.Count; i++)
            {
                for (var j = i + 1; j < tags.Count; j++)
                {
                    var key = string.Compare(tags[i], tags[j], StringComparison.Ordinal) <= 0
                        ? (tags[i], tags[j])
                        : (tags[j], tags[i]);

                    if (!comboData.ContainsKey(key))
                        comboData[key] = ([], [], []);

                    comboData[key].CombinedDayScores.Add(day.DayScore);
                }
            }
        }

        foreach (var day in byDay)
        {
            var tagSet = day.Tags.ToHashSet();

            foreach (var kvp in comboData)
            {
                var (t1, t2) = kvp.Key;
                var hasT1 = tagSet.Contains(t1);
                var hasT2 = tagSet.Contains(t2);
                if (hasT1 && !hasT2)
                    kvp.Value.Tag1AloneDayScores.Add(day.DayScore);
                else if (hasT2 && !hasT1)
                    kvp.Value.Tag2AloneDayScores.Add(day.DayScore);
            }
        }

        return comboData
            .Select(kvp => new TagComboDto(
                kvp.Key.Item1,
                kvp.Key.Item2,
                kvp.Value.CombinedDayScores.Average(),
                kvp.Value.Tag1AloneDayScores.Count == 0 ? 0.0 : kvp.Value.Tag1AloneDayScores.Average(),
                kvp.Value.Tag2AloneDayScores.Count == 0 ? 0.0 : kvp.Value.Tag2AloneDayScores.Average(),
                kvp.Value.CombinedDayScores.Count))
            .Where(c => c.CoOccurrences >= 3)
            .OrderByDescending(c => c.CoOccurrences)
            .ToList();
    }

    public async Task<IReadOnlyList<TagTrendSeriesDto>> GetTagTrendAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, Granularity period)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeTagTrend(events, period, GetTz());
    }

    internal static IReadOnlyList<TagTrendSeriesDto> ComputeTagTrend(IReadOnlyList<Event> events, Granularity period, TimeZoneInfo tz)
    {
        var tags = events
            .SelectMany(e => e.EventTags.Select(et => new { et.TagId, et.Tag.Name, e.Timestamp, e.Intensity }))
            .ToList();

        var top3 = tags
            .GroupBy(x => x.TagId)
            .Select(g => new { TagId = g.Key, TagName = g.First().Name, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .Take(3)
            .ToList();

        Func<DateTimeOffset, DateTimeOffset> getKey = period == Granularity.Week
            ? date => DateHelper.GetMonday(date, tz)
            : date => DateHelper.GetFirstDayOfMonth(date, tz);

        return top3.Select(tag =>
        {
            var points = tags
                .Where(e => e.TagId == tag.TagId)
                .GroupBy(e => getKey(e.Timestamp))
                .OrderBy(g => g.Key)
                .Select(g => new TagTrendPointDto(DateOnly.FromDateTime(g.Key.DateTime), g.Count(), g.Average(e => (double)e.Intensity)))
                .ToList();
            return new TagTrendSeriesDto(tag.TagName, points);
        }).ToList();
    }

    public async Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return ComputeInfluenceabilitySplit(events);
    }

    internal static InfluenceabilitySplitDto ComputeInfluenceabilitySplit(IReadOnlyList<Event> events)
    {
        var negEvents = events.Where(e => e.Type == EventType.Negative).ToList();
        var canInfluence = negEvents.Where(e => e.CanInfluence).ToList();
        var cannotInfluence = negEvents.Where(e => !e.CanInfluence).ToList();

        return new InfluenceabilitySplitDto(
            canInfluence.Count,
            canInfluence.Count == 0 ? 0.0 : canInfluence.Average(e => (double)e.Intensity),
            cannotInfluence.Count,
            cannotInfluence.Count == 0 ? 0.0 : cannotInfluence.Average(e => (double)e.Intensity));
    }
}
