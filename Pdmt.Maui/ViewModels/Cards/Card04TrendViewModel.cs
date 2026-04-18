using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record WeekBarItem(string WeekLabel, double PosBarHeight, double NegBarHeight, double Opacity);

public partial class Card04TrendViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxHeight = 160.0;

    [ObservableProperty] private IReadOnlyList<WeekBarItem> _weeks = [];
    [ObservableProperty] private bool _isEmpty;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var periods = await insightsService.GetTrendsAsync(from, to, "week", ct);

            IsEmpty = periods.Count == 0;
            double maxCount = periods.Count > 0
                ? (double)Math.Max(periods.Max(p => p.PosCount), periods.Max(p => p.NegCount))
                : 1;
            if (maxCount == 0) maxCount = 1;
            double maxIntensity = periods.Count > 0 ? periods.Max(p => p.AvgIntensity) : 1;
            if (maxIntensity == 0) maxIntensity = 1;

            Weeks = periods.Select(p => new WeekBarItem(
                $"{p.PeriodStart:dd.MM}–{p.PeriodStart.AddDays(6):dd.MM}",
                p.PosCount > 0 ? Math.Max(p.PosCount / maxCount * DesignMaxHeight, 4) : 0,
                p.NegCount > 0 ? Math.Max(p.NegCount / maxCount * DesignMaxHeight, 4) : 0,
                0.2 + 0.8 * p.AvgIntensity / maxIntensity)).ToList();
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
            ErrorMessage = "Не удалось загрузить тренды.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
