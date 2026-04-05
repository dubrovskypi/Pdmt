namespace Pdmt.Api.Dto.Analytics;

public record CalendarMonthDto(IReadOnlyList<CalendarDayLightDto> Days);

public record CalendarDayLightDto(DateTimeOffset Date, int PosCount, int NegCount, double DayScore);
