namespace Pdmt.Api.Dto.Insights;

public record TagComboDto(
    string Tag1,
    string Tag2,
    double CombinedAvgScore,
    double Tag1AloneAvgScore,
    double Tag2AloneAvgScore,
    int CoOccurrences);
