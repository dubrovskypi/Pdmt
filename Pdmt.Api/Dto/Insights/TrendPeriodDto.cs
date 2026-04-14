namespace Pdmt.Api.Dto.Insights;

public record TrendPeriodDto(DateOnly PeriodStart, int PosCount, int NegCount, double AvgIntensity);
