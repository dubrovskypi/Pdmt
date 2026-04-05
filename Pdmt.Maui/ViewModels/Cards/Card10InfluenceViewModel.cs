using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public partial class Card10InfluenceViewModel(InsightsService insightsService) : InsightCardViewModel
{
    private const double DesignTotalWidth = 240.0;

    [ObservableProperty] private int _canInfluenceCount;
    [ObservableProperty] private double _canInfluenceAvgIntensity;
    [ObservableProperty] private int _cannotInfluenceCount;
    [ObservableProperty] private double _cannotInfluenceAvgIntensity;
    [ObservableProperty] private double _canBarWidth;
    [ObservableProperty] private double _cannotBarWidth;

    public override async Task LoadAsync(DateTimeOffset from, DateTimeOffset to, bool showLoading = true, CancellationToken ct = default)
    {
        if (showLoading)
            IsLoading = true;
        ErrorMessage = null;
        try
        {
            var split = await insightsService.GetInfluenceabilityAsync(from, to, ct);
            if (split is null) return;

            CanInfluenceCount = split.CanInfluenceCount;
            CanInfluenceAvgIntensity = split.CanInfluenceAvgIntensity;
            CannotInfluenceCount = split.CannotInfluenceCount;
            CannotInfluenceAvgIntensity = split.CannotInfluenceAvgIntensity;

            var total = (double)(split.CanInfluenceCount + split.CannotInfluenceCount);
            if (total > 0)
            {
                CanBarWidth    = split.CanInfluenceCount    / total * DesignTotalWidth;
                CannotBarWidth = split.CannotInfluenceCount / total * DesignTotalWidth;
            }
            else
            {
                CanBarWidth    = 0;
                CannotBarWidth = 0;
            }
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
