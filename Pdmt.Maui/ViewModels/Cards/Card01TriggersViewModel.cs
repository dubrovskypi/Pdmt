using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record TriggerBarItem(string TagName, double AvgIntensity, double BarWidth);

public partial class Card01TriggersViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 160.0;

    [ObservableProperty] private IReadOnlyList<TriggerBarItem> _items = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var summary = await insightsService.GetWeeklySummaryAsync(from, ct);
            if (summary is null) return;

            var topNeg = summary.TopTags
                .OrderByDescending(t => t.AvgIntensity)
                .Take(5)
                .ToList();

            double max = topNeg.Count > 0 ? topNeg.Max(t => t.AvgIntensity) : 1;
            Items = topNeg.Select(tag => new TriggerBarItem(
                tag.TagName,
                tag.AvgIntensity,
                max > 0 ? tag.AvgIntensity / max * DesignMaxWidth : 0)).ToList();
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
}
