namespace Pdmt.Api.Dto.Insights;

public record WeekdayStatDto(string Day, int PosCount, int NegCount, double AvgIntensity);
