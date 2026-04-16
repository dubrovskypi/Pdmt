using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record TagTrendBarItem(string PeriodLabel, int Count, double BarHeight);
public record TagTrendSeries(string TagName, IReadOnlyList<TagTrendBarItem> Points);

public partial class Card09TagTrendViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxHeight = 80.0;

    [ObservableProperty] private IReadOnlyList<TagTrendSeries> _series = [];
    [ObservableProperty] private bool _isEmpty;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        IsEmpty = false;
        try
        {
            var seriesList = await insightsService.GetTagTrendAsync(from, to, "week", ct);

            if (seriesList.Count == 0)
            {
                IsEmpty = true;
                Series = [];
                return;
            }

            Series = seriesList.Take(3).Select(s =>
            {
                double maxCount = s.Points.Count > 0 ? s.Points.Max(p => (double)p.Count) : 1;
                var bars = s.Points.Select(p => new TagTrendBarItem(
                    p.PeriodStart.ToString("dd.MM"),
                    p.Count,
                    maxCount > 0 ? p.Count / maxCount * DesignMaxHeight : 4)).ToList();
                return new TagTrendSeries(s.TagName, bars);
            }).ToList();
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
