namespace Pdmt.Api.Dto.Analytics;

public record CalendarWeekDto(
    DateTimeOffset WeekStart,
    DateTimeOffset WeekEnd,
    IReadOnlyList<CalendarDayDetailsDto> Days);

public record CalendarDayDetailsDto(
    DateTimeOffset Date,
    int PosCount,
    int NegCount,
    int PositiveIntensitySum,
    int NegativeIntensitySum,
    double DayScore,
    IReadOnlyList<TagCountDto> TopPositiveTags,
    IReadOnlyList<TagCountDto> TopNegativeTags);

public record TagCountDto(string Name, int Count);
