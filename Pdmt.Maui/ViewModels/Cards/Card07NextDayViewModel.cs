using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record NextDayBarItem(string TagName, double NextDayAvgScore, int Occurrences, double BarWidth, bool IsPositive);

public partial class Card07NextDayViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 140.0;

    [ObservableProperty] private IReadOnlyList<NextDayBarItem> _items = [];

    public override async Task LoadAsync(DateTime from, DateTime to)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var effects = await insightsService.GetNextDayEffectsAsync(from, to);
            double maxAbs = effects.Count > 0
                ? effects.Max(e => Math.Abs(e.NextDayAvgScore))
                : 1;

            Items = effects.Take(6).Select(e => new NextDayBarItem(
                e.TagName,
                e.NextDayAvgScore,
                e.Occurrences,
                maxAbs > 0 ? Math.Abs(e.NextDayAvgScore) / maxAbs * DesignMaxWidth : 4,
                e.NextDayAvgScore >= 0)).ToList();
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
