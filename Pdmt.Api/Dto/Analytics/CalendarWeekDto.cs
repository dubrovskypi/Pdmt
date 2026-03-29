namespace Pdmt.Api.Dto.Analytics;

public record CalendarWeekDto(
    DateTime WeekStart,
    DateTime WeekEnd,
    IReadOnlyList<CalendarDayDetailsDto> Days);

public record CalendarDayDetailsDto(
    DateTime Date,
    int PosCount,
    int NegCount,
    int PositiveIntensitySum,
    int NegativeIntensitySum,
    double DayScore,
    IReadOnlyList<TagCountDto> TopPositiveTags,
    IReadOnlyList<TagCountDto> TopNegativeTags);

public record TagCountDto(string Name, int Count);
