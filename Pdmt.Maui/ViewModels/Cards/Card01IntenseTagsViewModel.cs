using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record TriggerBarItem(string TagName, double AvgIntensity, double BarWidth);

public partial class Card01IntenseTagsViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 160.0;

    [ObservableProperty] private IReadOnlyList<TriggerBarItem> _posItems = [];
    [ObservableProperty] private IReadOnlyList<TriggerBarItem> _negItems = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await insightsService.GetMostIntenseTagsAsync(from, to, ct);
            if (result is null) return;

            PosItems = BuildBars(result.TopPosTags);
            NegItems = BuildBars(result.TopNegTags);
        }
        catch (OperationCanceledException)
        {
            // Загрузка отменена — не показываем ошибку
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Исключение из-за отмены токена (например, SocketException) — не показываем ошибку
        }
        catch
        {
            ErrorMessage = "Не удалось загрузить триггеры.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private IReadOnlyList<TriggerBarItem> BuildBars(IReadOnlyList<Models.TagSummaryDto> tags)
    {
        double max = tags.Count > 0 ? tags.Max(t => t.AvgIntensity) : 1;
        return tags.Select(tag => new TriggerBarItem(
            tag.TagName,
            tag.AvgIntensity,
            max > 0 ? tag.AvgIntensity / max * DesignMaxWidth : 0)).ToList();
    }
}
