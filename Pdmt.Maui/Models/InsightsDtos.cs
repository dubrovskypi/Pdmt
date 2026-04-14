namespace Pdmt.Maui.Models;

// ── New insights endpoints ─────────────────────────────────────────────────

public record RepeatingTriggerDto(string TagName, int Count, double AvgIntensity);

public record DiscountedPositiveDto(string TagName, double AvgIntensity, int Count);

public record NextDayEffectDto(string TagName, double NextDayAvgScore, int Occurrences);

public record TagComboDto(
    string Tag1,
    string Tag2,
    double CombinedAvgIntensity,
    double Tag1AloneAvgIntensity,
    double Tag2AloneAvgIntensity,
    int CoOccurrences);

public record TagTrendPointDto(DateOnly PeriodStart, int Count, double AvgIntensity);

public record TagTrendSeriesDto(string TagName, List<TagTrendPointDto> Points);

public record InfluenceabilitySplitDto(
    int CanInfluenceCount,
    double CanInfluenceAvgIntensity,
    int CannotInfluenceCount,
    double CannotInfluenceAvgIntensity);

// ── Existing analytics endpoints (not yet in MAUI models) ─────────────────

public class WeeklySummaryDto
{
    public int PosCount { get; set; }
    public int NegCount { get; set; }
    public double AvgPosIntensity { get; set; }
    public double AvgNegIntensity { get; set; }
    public List<WeeklySummaryTagDto> TopTags { get; set; } = [];
    public List<DayOfWeekSummaryDto> ByDayOfWeek { get; set; } = [];
}

public class WeeklySummaryTagDto
{
    public required string TagName { get; set; }
    public int Count { get; set; }
    public double AvgIntensity { get; set; }
}

public class DayOfWeekSummaryDto
{
    public required string Day { get; set; }
    public int NegCount { get; set; }
    public int PosCount { get; set; }
    public double AvgIntensity { get; set; }
}

public class TrendPeriodDto
{
    public DateOnly PeriodStart { get; set; }
    public int PosCount { get; set; }
    public int NegCount { get; set; }
    public double AvgIntensity { get; set; }
}
