using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record ComboItem(
    string Label,
    double CombinedAvgScore,
    double AloneAvgScore,
    double CombinedBarWidth,
    double AloneBarWidth,
    int CoOccurrences);

public partial class Card08TagCombosViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 130.0;

    [ObservableProperty] private IReadOnlyList<ComboItem> _items = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var combos = await insightsService.GetTagCombosAsync(from, to, ct);
            double maxAbs = combos.Count > 0
                ? combos.Max(c => Math.Max(Math.Abs(c.CombinedAvgScore),
                    Math.Max(Math.Abs(c.Tag1AloneAvgScore), Math.Abs(c.Tag2AloneAvgScore))))
                : 1;
            if (maxAbs == 0) maxAbs = 1;

            Items = combos.Take(5).Select(c =>
            {
                var aloneScore = (c.Tag1AloneAvgScore + c.Tag2AloneAvgScore) / 2;
                return new ComboItem(
                    $"{c.Tag1} + {c.Tag2}",
                    c.CombinedAvgScore,
                    aloneScore,
                    Math.Abs(c.CombinedAvgScore) / maxAbs * DesignMaxWidth,
                    Math.Abs(aloneScore) / maxAbs * DesignMaxWidth,
                    c.CoOccurrences);
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
