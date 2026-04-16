namespace Pdmt.Maui.Models;

// ── Insights endpoints ─────────────────────────────────────────────────────

public record RepeatingTriggerDto(string TagName, int Count, double AvgIntensity);

public record DiscountedPositiveDto(string TagName, double AvgIntensity, int Count);

public record NextDayEffectDto(string TagName, double NextDayAvgScore, int Occurrences);

public record TagComboDto(
    string Tag1,
    string Tag2,
    double CombinedAvgScore,
    double Tag1AloneAvgScore,
    double Tag2AloneAvgScore,
    int CoOccurrences);

public record TagTrendPointDto(DateOnly PeriodStart, int Count, double AvgIntensity);

public record TagTrendSeriesDto(string TagName, List<TagTrendPointDto> Points);

public record InfluenceabilitySplitDto(
    int CanInfluenceCount,
    double CanInfluenceAvgIntensity,
    int CannotInfluenceCount,
    double CannotInfluenceAvgIntensity);

public record TagSummaryDto(string TagName, int Count, double AvgIntensity);

public record MostIntenseTagsDto(List<TagSummaryDto> TopPosTags, List<TagSummaryDto> TopNegTags);

public record PosNegBalanceDto(int PosCount, int NegCount, double AvgPosIntensity, double AvgNegIntensity);

public record WeekdayStatDto(string Day, int PosCount, int NegCount, double AvgIntensity);

public record TrendPeriodDto(DateOnly PeriodStart, int PosCount, int NegCount, double AvgIntensity);
