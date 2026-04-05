namespace Pdmt.Api.Dto.Analytics;

public record TagTrendPointDto(DateTimeOffset PeriodStart, int Count, double AvgIntensity);
