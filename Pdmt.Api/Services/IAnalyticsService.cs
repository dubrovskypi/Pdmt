using Pdmt.Api.Dto.Analytics;

namespace Pdmt.Api.Services;

public interface IAnalyticsService
{
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateTime weekOf);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTime from, DateTime to, TrendGranularity period);
    Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId);
    Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateTime weekOf);
    Task<CalendarMonthDto> GetCalendarMonthAsync(Guid userId, int year, int month);
    Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTime from, DateTime to, int minCount = 3);
    Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTime from, DateTime to);
    Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTime from, DateTime to);
    Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTime from, DateTime to);
    Task<IReadOnlyList<TagTrendPointDto>> GetTagTrendAsync(Guid userId, Guid tagId, DateTime from, DateTime to, TrendGranularity period);
    Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTime from, DateTime to);
}
