namespace Pdmt.Api.Dto.Analytics;

public record CorrelationsDto(
    string TagName,
    double AvgIntensityWithTag,
    double AvgIntensityWithoutTag,
    IReadOnlyList<DayFrequencyDto> DaysOfWeek);

public record DayFrequencyDto(string Day, int Frequency);
