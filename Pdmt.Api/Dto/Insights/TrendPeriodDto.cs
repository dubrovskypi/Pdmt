namespace Pdmt.Api.Dto.Insights;

public record TrendPeriodDto(DateTimeOffset PeriodStart, int PosCount, int NegCount, double AvgIntensity);
