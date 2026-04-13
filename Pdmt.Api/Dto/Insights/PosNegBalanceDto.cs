namespace Pdmt.Api.Dto.Insights;

public record PosNegBalanceDto(
int PosCount,
int NegCount,
double AvgPosIntensity,
double AvgNegIntensity);
