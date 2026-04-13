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
            var seriesList = await insightsService.GetTagTrendAsync(from, to, "week", ct);
            var top = seriesList.FirstOrDefault();
            if (top is null)
            {
                ErrorMessage = "Нет данных для тренда тега.";
                return;
            }

            TagName = top.TagName;
            double maxCount = top.Points.Count > 0 ? top.Points.Max(p => (double)p.Count) : 1;
            Points = top.Points.Select(p => new TagTrendBarItem(
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
