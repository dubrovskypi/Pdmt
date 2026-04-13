namespace Pdmt.Api.Dto.Analytics;

public record WeeklySummaryDto(
    int PosCount,
    int NegCount,
    double PosToNegRatio,
    double AvgPosIntensity,
    double AvgNegIntensity,
    IReadOnlyList<TagSummaryDto> TopTags,
    IReadOnlyList<TopEventDto> TopPosEvents,
    IReadOnlyList<TopEventDto> TopNegEvents,
    IReadOnlyList<DayOfWeekBreakdownDto> ByDayOfWeek);

public record TagSummaryDto(string TagName, int Count, double AvgIntensity);

public record TopEventDto(string? Title, int Intensity, DateTimeOffset Date);

public record DayOfWeekBreakdownDto(string Day, int PosCount, int NegCount, double AvgIntensity);
