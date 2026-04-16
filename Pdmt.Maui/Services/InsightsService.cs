using Pdmt.Maui.Models;
using System.Net.Http.Json;

namespace Pdmt.Maui.Services;

public class InsightsService(IHttpClientFactory factory)
{
    // ── Insights endpoints ─────────────────────────────────────────────────

    public async Task<MostIntenseTagsDto?> GetMostIntenseTagsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<MostIntenseTagsDto>(
            $"api/insights/most-intense-tags?from={f}&to={t}", ct);
    }

    public async Task<List<RepeatingTriggerDto>> GetRepeatingTriggersAsync(
        DateTimeOffset from, DateTimeOffset to, int minCount = 3, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<RepeatingTriggerDto>>(
            $"api/insights/repeating-triggers?from={f}&to={t}&minCount={minCount}", ct) ?? [];
    }

    public async Task<PosNegBalanceDto?> GetBalanceAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<PosNegBalanceDto>(
            $"api/insights/balance?from={f}&to={t}", ct);
    }

    public async Task<List<TrendPeriodDto>> GetTrendsAsync(
        DateTimeOffset from, DateTimeOffset to, string groupBy = "week", CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<TrendPeriodDto>>(
            $"api/insights/trends?from={f}&to={t}&period={groupBy}", ct) ?? [];
    }

    public async Task<List<DiscountedPositiveDto>> GetDiscountedPositivesAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<DiscountedPositiveDto>>(
            $"api/insights/discounted-positives?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<WeekdayStatDto>> GetWeekdayStatsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<WeekdayStatDto>>(
            $"api/insights/weekday-stats?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<NextDayEffectDto>> GetNextDayEffectsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<NextDayEffectDto>>(
            $"api/insights/next-day-effects?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<TagComboDto>> GetTagCombosAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<TagComboDto>>(
            $"api/insights/tag-combos?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<TagTrendSeriesDto>> GetTagTrendAsync(
        DateTimeOffset from, DateTimeOffset to, string period = "week", CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<TagTrendSeriesDto>>(
            $"api/insights/tag-trend?from={f}&to={t}&period={period}", ct) ?? [];
    }

    public async Task<InfluenceabilitySplitDto?> GetInfluenceabilityAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<InfluenceabilitySplitDto>(
            $"api/insights/influenceability?from={f}&to={t}", ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (string from, string to) FormatRange(DateTimeOffset from, DateTimeOffset to) => (
        Uri.EscapeDataString(from.ToUniversalTime().ToString("O")),
        Uri.EscapeDataString(to.ToUniversalTime().ToString("O")));
}
