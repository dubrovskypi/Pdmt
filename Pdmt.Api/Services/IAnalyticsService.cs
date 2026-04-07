using Pdmt.Api.Dto.Analytics;

namespace Pdmt.Api.Services;

public interface IAnalyticsService
{
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateOnly weekOf);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period);
    Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId, DateTimeOffset from, DateTimeOffset to);
    Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateOnly weekOf);
    Task<CalendarMonthDto> GetCalendarMonthAsync(Guid userId, int year, int month);
}
