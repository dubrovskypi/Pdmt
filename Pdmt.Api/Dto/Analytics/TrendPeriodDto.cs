namespace Pdmt.Api.Dto.Analytics;

public record TrendPeriodDto(DateTimeOffset PeriodStart, int PosCount, int NegCount, double AvgIntensity);
