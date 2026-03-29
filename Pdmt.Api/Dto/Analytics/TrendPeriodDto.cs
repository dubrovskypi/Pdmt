namespace Pdmt.Api.Dto.Analytics;

public record TrendPeriodDto(DateTime PeriodStart, int PosCount, int NegCount, double AvgIntensity);
