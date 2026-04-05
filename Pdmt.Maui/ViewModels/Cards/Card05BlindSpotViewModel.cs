using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record BlindSpotBarItem(string TagName, double AvgIntensity, int Count, double BarWidth);

public partial class Card05BlindSpotViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 160.0;

    [ObservableProperty] private IReadOnlyList<BlindSpotBarItem> _items = [];

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var positives = await insightsService.GetDiscountedPositivesAsync(from, to);
            double maxCount = positives.Count > 0 ? positives.Max(p => (double)p.Count) : 1;
            Items = positives.Select(p => new BlindSpotBarItem(
                p.TagName,
                p.AvgIntensity,
                p.Count,
                maxCount > 0 ? p.Count / maxCount * DesignMaxWidth : 0)).ToList();
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
