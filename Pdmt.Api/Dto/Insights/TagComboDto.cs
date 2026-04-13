namespace Pdmt.Api.Dto.Insights;

public record TagComboDto(
    string Tag1,
    string Tag2,
    double CombinedAvgIntensity,
    double Tag1AloneAvgIntensity,
    double Tag2AloneAvgIntensity,
    int CoOccurrences);
