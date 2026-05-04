using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public record BlindSpotBarItem(string TagName, double AvgIntensity, int Count, double BarWidth)
{
    public string FormattedAnnotation => $"{Count}× / {AvgIntensity:F1}";
}

public partial class Card05DiscountedPosViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignMaxWidth = 130.0;

    [ObservableProperty] private IReadOnlyList<BlindSpotBarItem> _items = [];
    [ObservableProperty] private bool _isEmpty;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var positives = await insightsService.GetDiscountedPositivesAsync(from, to, ct);
            IsEmpty = positives.Count == 0;
            double maxCount = positives.Count > 0 ? positives.Max(p => (double)p.Count) : 1;
            Items = positives.Select(p => new BlindSpotBarItem(
                p.TagName,
                p.AvgIntensity,
                p.Count,
                maxCount > 0 ? p.Count / maxCount * DesignMaxWidth : 0)).ToList();
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
