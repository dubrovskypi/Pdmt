using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record TagTrendBarItem(string PeriodLabel, int Count, double BarHeight, string BarColor);
public record TagTrendSeries(string TagName, string SeriesColor, IReadOnlyList<TagTrendBarItem> Points);

public partial class Card09TagTrendViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxHeight = 80.0;
    private static readonly string[] SeriesColors = ["#93c5fd", "#86efac", "#fca5a5"];

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

            Series = seriesList.Take(3).Select((s, idx) =>
            {
                var color = SeriesColors[idx % SeriesColors.Length];
                double maxCount = s.Points.Count > 0 ? s.Points.Max(p => (double)p.Count) : 1;
                var bars = s.Points.Select(p => new TagTrendBarItem(
                    $"{p.PeriodStart:dd.MM}–{p.PeriodStart.AddDays(6):dd.MM}",
                    p.Count,
                    maxCount > 0 ? Math.Max(p.Count / maxCount * DesignMaxHeight, p.Count > 0 ? 4 : 0) : 0,
                    color)).ToList();
                return new TagTrendSeries(s.TagName, color, bars);
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
