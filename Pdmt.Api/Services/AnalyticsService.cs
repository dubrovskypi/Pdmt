using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Infrastructure.Exceptions;

namespace Pdmt.Api.Services;

public class AnalyticsService(AppDbContext db) : IAnalyticsService
{
    private static DateTime GetMonday(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateTime weekOf)
    {
        var monday = GetMonday(DateTime.SpecifyKind(weekOf, DateTimeKind.Utc));

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= monday && e.Timestamp < monday.AddDays(7))
            .ToListAsync();

        var posEvents = events.Where(e => e.Type == 1).ToList();
        var negEvents = events.Where(e => e.Type == 0).ToList();

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
            .Select(e => new TopEventDto(e.Context, e.Intensity, e.Timestamp))
            .ToList();

        var topNegEvents = negEvents
            .OrderByDescending(e => e.Intensity)
            .Take(5)
            .Select(e => new TopEventDto(e.Context, e.Intensity, e.Timestamp))
            .ToList();

        var byDayOfWeek = events
            .GroupBy(e => e.Timestamp.DayOfWeek)
            .OrderBy(g => ((int)g.Key + 6) % 7)
            .Select(g => new DayOfWeekBreakdownDto(
                g.Key.ToString(),
                g.Count(e => e.Type == 0),
                g.Count(e => e.Type == 1),
                g.Average(e => (double)e.Intensity)))
            .ToList();

        return new WeeklySummaryDto(
            posEvents.Count,
            negEvents.Count,
            posToNegRatio,
            avgNegIntensity,
            avgPosIntensity,
            topTags,
            topPosEvents,
            topNegEvents,
            byDayOfWeek);
    }

    public async Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTime from, DateTime to, TrendGranularity period)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);
        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .Select(e => new { e.Timestamp, e.Type, e.Intensity })
            .ToListAsync();

        Func<DateTime, DateTime> getKey = period == TrendGranularity.Week
            ? date => GetMonday(date)
            : date => new DateTime(date.Year, date.Month, 1);

        return raw
            .GroupBy(e => getKey(e.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new TrendPeriodDto(
                g.Key,
                g.Count(e => e.Type == 1),
                g.Count(e => e.Type == 0),
                g.Average(e => (double)e.Intensity)))
            .ToList();
    }

    public async Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId)
    {
        var tag = await db.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId)
            ?? throw new NotFoundException("Tag not found.");

        // TODO: loads all user events — add date range filter when data grows
        var allEvents = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId)
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

    public async Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateTime weekOf)
    {
        var monday = GetMonday(DateTime.SpecifyKind(weekOf, DateTimeKind.Utc));

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= monday && e.Timestamp < monday.AddDays(7))
            .ToListAsync();

        var byDay = events.GroupBy(e => e.Timestamp.Date).ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<CalendarDayDetailsDto>(7);
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            if (!byDay.TryGetValue(date, out var dayEvents))
            {
                days.Add(new CalendarDayDetailsDto(date, 0, 0, 0, 0, 0.0, [], []));
                continue;
            }

            var posEvents = dayEvents.Where(e => e.Type == 1).ToList();
            var negEvents = dayEvents.Where(e => e.Type == 0).ToList();
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
        var from = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to)
            .Select(e => new { e.Timestamp, e.Type, e.Intensity })
            .ToListAsync();

        var byDay = raw.GroupBy(e => e.Timestamp.Date).ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<CalendarDayLightDto>(DateTime.DaysInMonth(year, month));
        for (var d = 1; d <= DateTime.DaysInMonth(year, month); d++)
        {
            var date = new DateTime(year, month, d);
            if (!byDay.TryGetValue(date, out var dayEvents))
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
                if (e.Type == 1) { posCount++; posSum += e.Intensity; }
                else { negCount++; negSum += e.Intensity; }
            }
            var total = dayEvents.Count;
            var dayScore = total == 0 ? 0.0 : (double)(posSum - negSum) / total;
            days.Add(new CalendarDayLightDto(date, posCount, negCount, dayScore));
        }

        return new CalendarMonthDto(days);
    }
    public async Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTime from, DateTime to, int minCount = 3)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Type == 0 && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return events
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() >= minCount)
            .OrderByDescending(g => g.Average(x => (double)x.Intensity))
            .Select(g => new RepeatingTriggerDto(g.Key, g.Count(), g.Average(x => (double)x.Intensity)))
            .ToList();
    }

    public async Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTime from, DateTime to)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Type == 1 && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return events
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() >= 5 && g.Average(x => (double)x.Intensity) < 4.0)
            .OrderByDescending(g => g.Count())
            .Select(g => new DiscountedPositiveDto(g.Key, g.Average(x => (double)x.Intensity), g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTime from, DateTime to)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        // Запрашиваем на 2 дня шире — чтобы вычислить dayScore следующего дня после последнего дня периода
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(2))
            .ToListAsync();

        // Вычисляем dayScore для каждого дня
        var dayScores = events
            .GroupBy(e => e.Timestamp.Date)
            .ToDictionary(
                g => g.Key,
                g => (double)(g.Sum(e => e.Type == 1 ? e.Intensity : 0) - g.Sum(e => e.Type == 0 ? e.Intensity : 0)) / g.Count());

        // Собираем теги только из основного периода [from, to]
        var tagDates = events
            .Where(e => e.Timestamp < to.AddDays(1))
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, Date = e.Timestamp.Date }))
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

    public async Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTime from, DateTime to)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        // Группируем события по дням
        var byDay = events
            .GroupBy(e => e.Timestamp.Date)
            .Select(g => new
            {
                Date = g.Key,
                Tags = g.SelectMany(e => e.EventTags.Select(et => et.Tag.Name)).Distinct().ToList(),
                AllIntensities = g.Select(e => e.Intensity).ToList()
            })
            .ToList();

        // Агрегируем данные по парам тегов
        var comboData = new Dictionary<(string, string), (List<int> CombinedDayIntensities, List<int> Tag1AloneDayIntensities, List<int> Tag2AloneDayIntensities)>();

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

                    comboData[key].CombinedDayIntensities.AddRange(day.AllIntensities);
                }
            }
        }

        // Вычисляем alone intensities отдельным проходом
        foreach (var day in byDay)
        {
            var tagSet = day.Tags.ToHashSet();
            foreach (var tag in day.Tags)
            {
                foreach (var otherTag in day.Tags)
                {
                    if (string.Compare(tag, otherTag, StringComparison.Ordinal) >= 0) continue;
                    var key = (tag, otherTag);
                    if (!comboData.ContainsKey(key)) continue;
                    // Эта пара co-occurred — уже обработано в CombinedDayIntensities
                }
            }

            // Alone: день, где Tag есть, но парного тега нет
            foreach (var kvp in comboData)
            {
                var (t1, t2) = kvp.Key;
                var hasT1 = tagSet.Contains(t1);
                var hasT2 = tagSet.Contains(t2);
                if (hasT1 && !hasT2)
                    kvp.Value.Tag1AloneDayIntensities.AddRange(day.AllIntensities);
                else if (hasT2 && !hasT1)
                    kvp.Value.Tag2AloneDayIntensities.AddRange(day.AllIntensities);
            }
        }

        return comboData
            .Where(kvp => kvp.Value.CombinedDayIntensities.Count > 0)
            .Select(kvp =>
            {
                var combinedDays = byDay.Count(d =>
                    d.Tags.Contains(kvp.Key.Item1) && d.Tags.Contains(kvp.Key.Item2));
                return new TagComboDto(
                    kvp.Key.Item1,
                    kvp.Key.Item2,
                    kvp.Value.CombinedDayIntensities.Average(x => (double)x),
                    kvp.Value.Tag1AloneDayIntensities.Count == 0 ? 0.0 : kvp.Value.Tag1AloneDayIntensities.Average(x => (double)x),
                    kvp.Value.Tag2AloneDayIntensities.Count == 0 ? 0.0 : kvp.Value.Tag2AloneDayIntensities.Average(x => (double)x),
                    combinedDays);
            })
            .Where(c => c.CoOccurrences >= 3)
            .OrderByDescending(c => c.CoOccurrences)
            .ToList();
    }

    public async Task<IReadOnlyList<TagTrendPointDto>> GetTagTrendAsync(Guid userId, Guid tagId, DateTime from, DateTime to, TrendGranularity period)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        _ = await db.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId)
            ?? throw new NotFoundException("Tag not found.");

        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId
                && e.Timestamp >= from && e.Timestamp < to.AddDays(1)
                && e.EventTags.Any(et => et.TagId == tagId))
            .Select(e => new { e.Timestamp, e.Intensity })
            .ToListAsync();

        Func<DateTime, DateTime> getKey = period == TrendGranularity.Week
            ? date => GetMonday(date)
            : date => new DateTime(date.Year, date.Month, 1);

        return raw
            .GroupBy(e => getKey(e.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new TagTrendPointDto(g.Key, g.Count(), g.Average(e => (double)e.Intensity)))
            .ToList();
    }

    public async Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTime from, DateTime to)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Type == 0 && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .Select(e => new { e.CanInfluence, e.Intensity })
            .ToListAsync();

        var canInfluence = raw.Where(e => e.CanInfluence).ToList();
        var cannotInfluence = raw.Where(e => !e.CanInfluence).ToList();

        return new InfluenceabilitySplitDto(
            canInfluence.Count,
            canInfluence.Count == 0 ? 0.0 : canInfluence.Average(e => (double)e.Intensity),
            cannotInfluence.Count,
            cannotInfluence.Count == 0 ? 0.0 : cannotInfluence.Average(e => (double)e.Intensity));
    }
}
