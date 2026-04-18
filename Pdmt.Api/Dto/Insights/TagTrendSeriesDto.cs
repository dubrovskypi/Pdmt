namespace Pdmt.Api.Dto.Insights;

public record TagTrendSeriesDto(string TagName, IReadOnlyList<TagTrendPointDto> Points);
public record TagTrendPointDto(DateOnly PeriodStart, int Count, double AvgIntensity);
