namespace Pdmt.Api.Dto.Insights;

public record TagTrendSeriesDto(string TagName, IReadOnlyList<TagTrendPointDto> Points);
public record TagTrendPointDto(DateTimeOffset PeriodStart, int Count, double AvgIntensity);
