using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Analytics;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;

namespace Pdmt.Api.Services;

public class InsightsService(AppDbContext db, IConfiguration config) : IInsightsService
{
    private TimeZoneInfo GetTz() =>
        TimeZoneInfo.FindSystemTimeZoneById(config["App:DefaultTimeZone"]!);

    public async Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, int minCount = 3)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Type == EventType.Negative && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return events
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() >= minCount)
            .OrderByDescending(g => g.Average(x => (double)x.Intensity))
            .Select(g => new RepeatingTriggerDto(g.Key, g.Count(), g.Average(x => (double)x.Intensity)))
            .ToList();
    }

    public async Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var events = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags).ThenInclude(et => et.Tag)
            .Where(e => e.UserId == userId && e.Type == EventType.Positive && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
            .ToListAsync();

        return events
            .SelectMany(e => e.EventTags.Select(et => new { et.Tag.Name, e.Intensity }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() >= 5 && g.Average(x => (double)x.Intensity) < 4.0)
            .OrderByDescending(g => g.Count())
            .Select(g => new DiscountedPositiveDto(g.Key, g.Average(x => (double)x.Intensity), g.Count()))
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

        // Вычисляем dayScore для каждого дня
        var tz = GetTz();
        var dayScores = events
            .GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz))
            .ToDictionary(
                g => g.Key,
                g => (double)(g.Sum(e => e.Type == EventType.Positive ? e.Intensity : 0) - g.Sum(e => e.Type == EventType.Negative ? e.Intensity : 0)) / g.Count());

        // Собираем теги только из основного периода [from, to]
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

        // Группируем события по дням
        var tz = GetTz();
        var byDay = events
            .GroupBy(e => DateHelper.ToLocalDate(e.Timestamp, tz))
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

        // Второй проход: заполняем alone intensities для дней, где один тег есть, а другой нет
        foreach (var day in byDay)
        {
            var tagSet = day.Tags.ToHashSet();

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

    public async Task<IReadOnlyList<TagTrendPointDto>> GetTagTrendAsync(Guid userId, Guid tagId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period)
    {
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

        Func<DateTimeOffset, DateTimeOffset> getKey = period == TrendGranularity.Week
            ? date => DateHelper.GetMonday(date)
            : date => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return raw
            .GroupBy(e => getKey(e.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new TagTrendPointDto(g.Key, g.Count(), g.Average(e => (double)e.Intensity)))
            .ToList();
    }

    public async Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var raw = await db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Type == EventType.Negative && e.Timestamp >= from && e.Timestamp < to.AddDays(1))
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
