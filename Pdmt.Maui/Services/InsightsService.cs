using Pdmt.Maui.Models;
using System.Net.Http.Json;

namespace Pdmt.Maui.Services;

public class InsightsService(IHttpClientFactory factory)
{
    // ── Insights endpoints ─────────────────────────────────────────────────

    public async Task<List<RepeatingTriggerDto>> GetRepeatingTriggersAsync(
        DateTimeOffset from, DateTimeOffset to, int minCount = 3, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<RepeatingTriggerDto>>(
            $"api/analytics/insights/repeating-triggers?from={f}&to={t}&minCount={minCount}", ct) ?? [];
    }

    public async Task<List<DiscountedPositiveDto>> GetDiscountedPositivesAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<DiscountedPositiveDto>>(
            $"api/analytics/insights/discounted-positives?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<NextDayEffectDto>> GetNextDayEffectsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<NextDayEffectDto>>(
            $"api/analytics/insights/next-day-effects?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<TagComboDto>> GetTagCombosAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<TagComboDto>>(
            $"api/analytics/insights/tag-combos?from={f}&to={t}", ct) ?? [];
    }

    public async Task<List<TagTrendSeriesDto>> GetTagTrendAsync(
        DateTimeOffset from, DateTimeOffset to, string period = "week", CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<TagTrendSeriesDto>>(
            $"api/analytics/insights/tag-trend?from={f}&to={t}&period={period}", ct) ?? [];
    }

    public async Task<InfluenceabilitySplitDto?> GetInfluenceabilityAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<InfluenceabilitySplitDto>(
            $"api/analytics/insights/influenceability?from={f}&to={t}", ct);
    }

    // ── Existing analytics endpoints ───────────────────────────────────────

    public async Task<WeeklySummaryDto?> GetWeeklySummaryAsync(DateTimeOffset weekOf, CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var param = Uri.EscapeDataString(weekOf.ToUniversalTime().ToString("yyyy-MM-dd"));
        return await http.GetFromJsonAsync<WeeklySummaryDto>(
            $"api/analytics/weekly-summary?weekOf={param}", ct);
    }

    public async Task<List<TrendPeriodDto>> GetTrendsAsync(
        DateTimeOffset from, DateTimeOffset to, string groupBy = "week", CancellationToken ct = default)
    {
        var http = factory.CreateClient("PdmtApi");
        var (f, t) = FormatRange(from, to);
        return await http.GetFromJsonAsync<List<TrendPeriodDto>>(
            $"api/analytics/trends?from={f}&to={t}&period={groupBy}", ct) ?? [];
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (string from, string to) FormatRange(DateTimeOffset from, DateTimeOffset to) => (
        Uri.EscapeDataString(from.ToUniversalTime().ToString("O")),
        Uri.EscapeDataString(to.ToUniversalTime().ToString("O")));
}
