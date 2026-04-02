using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record RepeatingBarItem(string TagName, int Count, double BarWidth);

public partial class Card02RepeatingViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 160.0;

    [ObservableProperty] private IReadOnlyList<RepeatingBarItem> _items = [];

    public override async Task LoadAsync(DateTime from, DateTime to)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var triggers = await insightsService.GetRepeatingTriggersAsync(from, to);
            double max = triggers.Count > 0 ? triggers.Max(t => (double)t.Count) : 1;
            Items = triggers.Select(t => new RepeatingBarItem(
                t.TagName,
                t.Count,
                max > 0 ? t.Count / max * DesignMaxWidth : 0)).ToList();
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
