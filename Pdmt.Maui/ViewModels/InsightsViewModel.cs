using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;
using Pdmt.Maui.ViewModels.Cards;
using System.Collections.ObjectModel;

namespace Pdmt.Maui.ViewModels;

public partial class InsightsViewModel : ObservableObject
{
    public record PeriodOption(string Label, int Days);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekSelected))]
    [NotifyPropertyChangedFor(nameof(IsTwoWeeksSelected))]
    [NotifyPropertyChangedFor(nameof(IsMonthSelected))]
    private PeriodOption _selectedPeriod;

    [ObservableProperty]
    private bool _isPageLoading;

    private CancellationTokenSource? _cts;

    public IReadOnlyList<PeriodOption> PeriodOptions { get; } = [
        new("Неделя", 7),
        new("2 недели", 14),
        new("Месяц", 30),
    ];

    public ObservableCollection<InsightCardViewModel> Cards { get; }

    public bool IsWeekSelected => SelectedPeriod == PeriodOptions[0];
    public bool IsTwoWeeksSelected => SelectedPeriod == PeriodOptions[1];
    public bool IsMonthSelected => SelectedPeriod == PeriodOptions[2];

    public InsightsViewModel(InsightsService insightsService)
    {
        _selectedPeriod = PeriodOptions[0];

        Cards = [
            new Card01IntenseTagsViewModel(insightsService),
            new Card02RepeatingViewModel(insightsService),
            new Card03BalanceViewModel(insightsService),
            new Card04TrendViewModel(insightsService),
            new Card05DiscountedPosViewModel(insightsService),
            new Card06WeekdaysViewModel(insightsService),
            new Card07NextDayViewModel(insightsService),
            new Card08TagCombosViewModel(insightsService),
            new Card09TagTrendViewModel(insightsService),
            new Card10InfluenceViewModel(insightsService),
        ];
    }

    public void CancelLoad()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Отменяем предыдущую загрузку при быстром переключении периода
        CancelLoad();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-SelectedPeriod.Days);

        IsPageLoading = true;
        try
        {
            // Приоритет: загружаем карточки 0–1 синхронно
            // showLoading: false чтобы per-card спиннеры не отвлекали от страничного
            await Task.WhenAll(Cards.Take(2).Select(c => c.LoadAsync(from, to, showLoading: false, ct)));

            // Страница готова — убираем спиннер
            IsPageLoading = false;

            // Остальные 8 карточек в фоне (с per-card spinner),
            // но всё ещё в await'е чтобы CancellationToken их отменял
            await Task.WhenAll(Cards.Skip(2).Select(c => c.LoadAsync(from, to, showLoading: true, ct)));
        }
        catch (OperationCanceledException)
        {
            // Загрузка отменена (уход со страницы или смена периода) — это норма
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Исключение из-за отмены токена (например, SocketException) — не показываем ошибку
        }
        finally
        {
            IsPageLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectPeriodAsync(PeriodOption period)
    {
        if (period == SelectedPeriod) return;
        SelectedPeriod = period;
        await LoadAsync();
    }
}
