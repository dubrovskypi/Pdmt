namespace Pdmt.Api.Dto.Analytics;

public record InfluenceabilitySplitDto(
    int CanInfluenceCount,
    double CanInfluenceAvgIntensity,
    int CannotInfluenceCount,
    double CannotInfluenceAvgIntensity);
