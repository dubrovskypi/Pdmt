using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record RepeatingBarItem(string TagName, int Count, double BarWidth);

public partial class Card02RepeatingViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 160.0;

    [ObservableProperty] private IReadOnlyList<RepeatingBarItem> _items = [];
    [ObservableProperty] private bool _isEmpty;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var triggers = await insightsService.GetRepeatingTriggersAsync(from, to, ct: ct);
            IsEmpty = triggers.Count == 0;
            double max = triggers.Count > 0 ? triggers.Max(t => (double)t.Count) : 1;
            Items = triggers.Select(t => new RepeatingBarItem(
                t.TagName,
                t.Count,
                max > 0 ? t.Count / max * DesignMaxWidth : 0)).ToList();
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
