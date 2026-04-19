using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record TriggerBarItem(string TagName, double AvgIntensity, double BarProgress, Color BarColor);

public partial class Card01IntenseTagsViewModel(InsightsService insightsService) : InsightCardViewModel
{
    [ObservableProperty] private IReadOnlyList<TriggerBarItem> _posItems = [];
    [ObservableProperty] private IReadOnlyList<TriggerBarItem> _negItems = [];
    [ObservableProperty] private bool _isEmpty = true;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await insightsService.GetMostIntenseTagsAsync(from, to, ct);
            if (result is null) return;

            PosItems = BuildBars(result.TopPosTags, Color.FromArgb("#4ade80"));
            NegItems = BuildBars(result.TopNegTags, Color.FromArgb("#f87171"));
            IsEmpty = PosItems.Count == 0 && NegItems.Count == 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception) when (ct.IsCancellationRequested) { }
        catch
        {
            ErrorMessage = "Не удалось загрузить триггеры.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static IReadOnlyList<TriggerBarItem> BuildBars(IReadOnlyList<Models.TagSummaryDto> tags, Color barColor)
    {
        double max = tags.Count > 0 ? tags.Max(t => t.AvgIntensity) : 1;
        return tags.Select(t => new TriggerBarItem(
            t.TagName,
            t.AvgIntensity,
            max > 0 ? t.AvgIntensity / max : 0,
            barColor)).ToList();
    }
}
