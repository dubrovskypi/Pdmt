namespace Pdmt.Api.Dto.Analytics;

public record WeeklySummaryDto(
    int PosCount,
    int NegCount,
    double PosToNegRatio,
    double AvgNegIntensity,
    double AvgPosIntensity,
    IReadOnlyList<TagSummaryDto> TopTags,
    IReadOnlyList<TopEventDto> TopPosEvents,
    IReadOnlyList<TopEventDto> TopNegEvents,
    IReadOnlyList<DayOfWeekBreakdownDto> ByDayOfWeek);

public record TagSummaryDto(string TagName, int Count, double AvgIntensity);

public record TopEventDto(string? Title, int Intensity, DateTime Date);

public record DayOfWeekBreakdownDto(string Day, int NegCount, int PosCount, double AvgIntensity);
