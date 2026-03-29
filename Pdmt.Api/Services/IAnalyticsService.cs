using Pdmt.Api.Dto.Analytics;

namespace Pdmt.Api.Services;

public interface IAnalyticsService
{
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateTime weekOf);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTime from, DateTime to, TrendGranularity period);
    Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId);
    Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateTime weekOf);
    Task<CalendarMonthDto> GetCalendarMonthAsync(Guid userId, int year, int month);
}
