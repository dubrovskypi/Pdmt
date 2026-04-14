using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record WeekBarItem(string WeekLabel, int PosCount, int NegCount, double BarHeight, bool IsPositive);

public partial class Card04TrendViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxHeight = 80.0;

    [ObservableProperty] private IReadOnlyList<WeekBarItem> _weeks = [];
    [ObservableProperty] private bool _isEmpty;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var rangeFrom = to.AddDays(-42);
            var periods = await insightsService.GetTrendsAsync(rangeFrom, to, "week", ct);

            IsEmpty = periods.Count == 0;
            double maxNet = periods.Count > 0
                ? periods.Max(p => (double)Math.Abs(p.PosCount - p.NegCount))
                : 1;

            Weeks = periods.TakeLast(6).Select(p =>
            {
                var net = p.PosCount - p.NegCount;
                return new WeekBarItem(
                    p.PeriodStart.ToString("dd.MM"),
                    p.PosCount,
                    p.NegCount,
                    maxNet > 0 ? Math.Abs(net) / maxNet * DesignMaxHeight : 4,
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
            ErrorMessage = "Не удалось загрузить тренды.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
