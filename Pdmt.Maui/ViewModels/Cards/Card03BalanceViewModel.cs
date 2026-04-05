using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public partial class Card03BalanceViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 140.0;

    [ObservableProperty] private int _posCount;
    [ObservableProperty] private int _negCount;
    [ObservableProperty] private double _avgPosIntensity;
    [ObservableProperty] private double _avgNegIntensity;
    [ObservableProperty] private double _posBarWidth;
    [ObservableProperty] private double _negBarWidth;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var summary = await insightsService.GetWeeklySummaryAsync(from);
            if (summary is null) return;

            PosCount = summary.PosCount;
            NegCount = summary.NegCount;
            AvgPosIntensity = summary.AvgPosIntensity;
            AvgNegIntensity = summary.AvgNegIntensity;

            double maxIntensity = Math.Max(summary.AvgPosIntensity, summary.AvgNegIntensity);
            PosBarWidth = maxIntensity > 0 ? summary.AvgPosIntensity / maxIntensity * DesignMaxWidth : 0;
            NegBarWidth = maxIntensity > 0 ? summary.AvgNegIntensity / maxIntensity * DesignMaxWidth : 0;
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
