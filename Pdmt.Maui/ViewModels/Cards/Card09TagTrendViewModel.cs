using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record TagTrendBarItem(string PeriodLabel, int Count, double BarHeight);

public partial class Card09TagTrendViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxHeight = 80.0;

    [ObservableProperty] private string? _tagName;
    [ObservableProperty] private IReadOnlyList<TagTrendBarItem> _points = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        TagName = null;
        try
        {
            var triggers = await insightsService.GetRepeatingTriggersAsync(from, to, minCount: 1, ct: ct);
            var topTrigger = triggers.MaxBy(t => t.Count);
            if (topTrigger is null)
            {
                ErrorMessage = "Нет данных для тренда тега.";
                return;
            }

            var tagId = await insightsService.FindTagIdByNameAsync(topTrigger.TagName);
            if (tagId is null)
            {
                ErrorMessage = "Тег не найден.";
                return;
            }

            TagName = topTrigger.TagName;
            var rawPoints = await insightsService.GetTagTrendAsync(tagId.Value, from, to, "week", ct);

            double maxCount = rawPoints.Count > 0 ? rawPoints.Max(p => (double)p.Count) : 1;
            Points = rawPoints.Select(p => new TagTrendBarItem(
                p.PeriodStart.ToString("dd.MM"),
                p.Count,
                maxCount > 0 ? p.Count / maxCount * DesignMaxHeight : 4)).ToList();
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
            ErrorMessage = "Не удалось загрузить тренд тега.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
