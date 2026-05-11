using Pdmt.Api.Dto.Analytics;

namespace Pdmt.Api.Services;

public interface IAnalyticsService
{
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, DateOnly weekOf, CancellationToken ct);
    Task<CorrelationsDto> GetCorrelationsAsync(Guid userId, Guid tagId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<CalendarWeekDto> GetCalendarWeekAsync(Guid userId, DateOnly weekOf, CancellationToken ct);
    Task<CalendarMonthDto> GetCalendarMonthAsync(Guid userId, int year, int month, CancellationToken ct);
}
