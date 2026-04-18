namespace Pdmt.Api.Dto.Insights;

public record InfluenceabilitySplitDto(
    int CanInfluenceCount,
    double CanInfluenceAvgIntensity,
    int CannotInfluenceCount,
    double CannotInfluenceAvgIntensity);
