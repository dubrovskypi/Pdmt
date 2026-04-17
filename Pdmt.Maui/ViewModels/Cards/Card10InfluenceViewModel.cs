using CommunityToolkit.Mvvm.ComponentModel;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels.Cards;

public partial class Card10InfluenceViewModel(InsightsService insightsService) : InsightCardViewModel
{
    [ObservableProperty] private int _canInfluenceCount;
    [ObservableProperty] private double _canInfluenceAvgIntensity;
    [ObservableProperty] private int _cannotInfluenceCount;
    [ObservableProperty] private double _cannotInfluenceAvgIntensity;
    [ObservableProperty] private GridLength _canGridLength = new(1, GridUnitType.Star);
    [ObservableProperty] private GridLength _cannotGridLength = new(1, GridUnitType.Star);

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
            CanGridLength = new GridLength(split.CanInfluenceCount, GridUnitType.Star);
            CannotGridLength = new GridLength(split.CannotInfluenceCount, GridUnitType.Star);
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
