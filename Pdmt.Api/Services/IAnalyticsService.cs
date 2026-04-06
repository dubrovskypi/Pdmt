using Pdmt.Api.Dto.Analytics;

namespace Pdmt.Api.Services;

public interface IAnalyticsService
{
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateOnly weekOf);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period);
    Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId, DateTimeOffset from, DateTimeOffset to);
    Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateOnly weekOf);
    Task<CalendarMonthDto> GetCalendarMonthAsync(Guid userId, int year, int month);
    Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, int minCount = 3);
    Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    /// <summary>
    /// Returns tag combinations that co-occur on 3+ days within a date range.
    /// If a tag pair only co-occurs together and never appears separately,
    /// Tag1AloneAvgIntensity and Tag2AloneAvgIntensity will both be 0.0 (expected behavior).
    /// </summary>
    Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TagTrendPointDto>> GetTagTrendAsync(Guid userId, Guid tagId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period);
    Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
}
