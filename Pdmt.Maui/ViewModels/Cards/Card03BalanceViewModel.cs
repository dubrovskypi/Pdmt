using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public partial class Card03BalanceViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 160.0;

    [ObservableProperty] private int _posCount;
    [ObservableProperty] private int _negCount;
    [ObservableProperty] private double _avgPosIntensity;
    [ObservableProperty] private double _avgNegIntensity;
    [ObservableProperty] private double _posBarWidth;
    [ObservableProperty] private double _negBarWidth;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var balance = await insightsService.GetBalanceAsync(from, to, ct);
            if (balance is null) return;

            PosCount = balance.PosCount;
            NegCount = balance.NegCount;
            AvgPosIntensity = balance.AvgPosIntensity;
            AvgNegIntensity = balance.AvgNegIntensity;

            double maxIntensity = Math.Max(balance.AvgPosIntensity, balance.AvgNegIntensity);
            PosBarWidth = maxIntensity > 0 ? balance.AvgPosIntensity / maxIntensity * DesignMaxWidth : 0;
            NegBarWidth = maxIntensity > 0 ? balance.AvgNegIntensity / maxIntensity * DesignMaxWidth : 0;
        }
        catch (OperationCanceledException)
        {
            // Load cancelled — do not show error
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Exception due to token cancellation (e.g. SocketException) — do not show error
        }
        catch
        {
            ErrorMessage = LoadErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
