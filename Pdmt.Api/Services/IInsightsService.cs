using Pdmt.Api.Dto.Insights;

namespace Pdmt.Api.Services;

public interface IInsightsService
{
    Task<MostIntenseTagsDto> GetMostIntenseTagsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, int minCount = 3);
    Task<PosNegBalanceDto> GetBalanceAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, Granularity period);
    Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<WeekdayStatDto>> GetWeekdayStatsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<TagTrendSeriesDto>> GetTagTrendAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, Granularity period);
    Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTimeOffset from, DateTimeOffset to);
}
