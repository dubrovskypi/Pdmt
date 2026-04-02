using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record ComboItem(
    string Label,
    double CombinedAvgIntensity,
    double AloneAvgIntensity,
    double CombinedBarWidth,
    double AloneBarWidth,
    int CoOccurrences);

public partial class Card08CombosViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 130.0;

    [ObservableProperty] private IReadOnlyList<ComboItem> _items = [];

    public override async Task LoadAsync(DateTime from, DateTime to)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var combos = await insightsService.GetTagCombosAsync(from, to);
            double max = combos.Count > 0
                ? combos.Max(c => Math.Max(c.CombinedAvgIntensity,
                    Math.Max(c.Tag1AloneAvgIntensity, c.Tag2AloneAvgIntensity)))
                : 1;

            Items = combos.Take(5).Select(c =>
            {
                var alone = (c.Tag1AloneAvgIntensity + c.Tag2AloneAvgIntensity) / 2;
                return new ComboItem(
                    $"{c.Tag1} + {c.Tag2}",
                    c.CombinedAvgIntensity,
                    alone,
                    max > 0 ? c.CombinedAvgIntensity / max * DesignMaxWidth : 0,
                    max > 0 ? alone / max * DesignMaxWidth : 0,
                    c.CoOccurrences);
            }).ToList();
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
