namespace Pdmt.Api.Dto.Analytics;

public record TrendPeriodDto(DateOnly PeriodStart, int PosCount, int NegCount, double AvgIntensity);
