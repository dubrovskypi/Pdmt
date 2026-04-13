namespace Pdmt.Api.Dto.Insights;

public record MostIntenseTagsDto(IReadOnlyList<TagSummaryDto> TopPosTags, IReadOnlyList<TagSummaryDto> TopNegTags);
public record TagSummaryDto(string TagName, int Count, double AvgIntensity);