using Pdmt.Api.Dto.Insights;

namespace Pdmt.Api.Services;

public interface IInsightsService
{
    Task<MostIntenseTagsDto> GetMostIntenseTagsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, int minCount, CancellationToken ct);
    Task<PosNegBalanceDto> GetBalanceAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<TrendPeriodDto>> GetTrendsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, Granularity period, CancellationToken ct);
    Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<WeekdayStatDto>> GetWeekdayStatsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<TagTrendSeriesDto>> GetTagTrendAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, Granularity period, CancellationToken ct);
    Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
