using Pdmt.Api.Dto.Analytics;

namespace Pdmt.Api.Services;

public interface IInsightsService
{
    Task<TriggersDto> GetMaxIntensiveTagsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, int minCount = 3);
    Task<BalanceDto> GetBalanceAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period);
    Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<WeekdayStatsDto>> GetWeekdayStatsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TagTrendSeriesDto>> GetTagTrendAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, TrendGranularity period);
    Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
}
