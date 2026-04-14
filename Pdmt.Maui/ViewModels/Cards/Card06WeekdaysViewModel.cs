using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record DayBarItem(string DayLabel, double AvgScore, double BarWidth, bool IsPositive);

public partial class Card06WeekdaysViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 140.0;

    [ObservableProperty] private IReadOnlyList<DayBarItem> _days = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var summary = await insightsService.GetWeeklySummaryAsync(from, ct);
            if (summary is null) return;

            double maxAbs = summary.ByDayOfWeek.Count > 0
                ? summary.ByDayOfWeek.Max(d => Math.Abs(d.PosCount - d.NegCount) > 0
                    ? (double)Math.Abs(d.PosCount - d.NegCount)
                    : 1)
                : 1;

            Days = summary.ByDayOfWeek.Select(day =>
            {
                var net = day.PosCount - day.NegCount;
                return new DayBarItem(
                    day.Day[..3],
                    day.AvgIntensity,
                    maxAbs > 0 ? Math.Abs(net) / maxAbs * DesignMaxWidth : 4,
                    net >= 0);
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
            ErrorMessage = "Не удалось загрузить данные.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
