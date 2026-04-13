namespace Pdmt.Api.Dto.Analytics;

public record TagTrendSeriesDto(string TagName, IReadOnlyList<TagTrendPointDto> Points);
