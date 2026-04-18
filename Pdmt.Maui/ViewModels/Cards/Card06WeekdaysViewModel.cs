using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record DayBarItem(string DayLabel, double AvgScore, double BarWidth, string BarColor);

public partial class Card06WeekdaysViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 140.0;

    private static string GetBarColor(int pos, int neg)
    {
        var total = pos + neg;
        if (total == 0) return "#94a3b8";
        var ratio = (double)pos / total;
        return ratio switch
        {
            >= 0.85 => "#22c55e",
            >= 0.70 => "#4ade80",
            >= 0.55 => "#a3e635",
            >= 0.45 => "#fbbf24",
            >= 0.30 => "#fb923c",
            >= 0.15 => "#f87171",
            _       => "#ef4444",
        };
    }

    [ObservableProperty] private IReadOnlyList<DayBarItem> _days = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var stats = await insightsService.GetWeekdayStatsAsync(from, to, ct);

            double maxIntensity = stats.Count > 0
                ? Math.Max(stats.Max(d => d.AvgIntensity), 1)
                : 1;

            Days = stats.Select(day => new DayBarItem(
                day.Day[..3],
                day.AvgIntensity,
                day.AvgIntensity / maxIntensity * DesignMaxWidth,
                GetBarColor(day.PosCount, day.NegCount))).ToList();
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
            ErrorMessage = "Не удалось загрузить данные.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
