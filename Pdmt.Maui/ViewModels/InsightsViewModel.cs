using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;
using Pdmt.Maui.ViewModels.Cards;

namespace Pdmt.Maui.ViewModels;

public partial class InsightsViewModel : ObservableObject
{
    public record PeriodOption(string Label, int Days);

    public IReadOnlyList<PeriodOption> PeriodOptions { get; } = [
        new("Неделя", 7),
        new("2 недели", 14),
        new("Месяц", 30),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekSelected))]
    [NotifyPropertyChangedFor(nameof(IsTwoWeeksSelected))]
    [NotifyPropertyChangedFor(nameof(IsMonthSelected))]
    private PeriodOption _selectedPeriod;

    public bool IsWeekSelected     => SelectedPeriod == PeriodOptions[0];
    public bool IsTwoWeeksSelected => SelectedPeriod == PeriodOptions[1];
    public bool IsMonthSelected    => SelectedPeriod == PeriodOptions[2];

    public ObservableCollection<InsightCardViewModel> Cards { get; }

    [ObservableProperty] private bool _isPageLoading;

    public InsightsViewModel(InsightsService insightsService)
    {
        _selectedPeriod = PeriodOptions[0];

        Cards = [
            new Card01TriggersViewModel(insightsService),
            new Card02RepeatingViewModel(insightsService),
            new Card03BalanceViewModel(insightsService),
            new Card04TrendRatioViewModel(insightsService),
            new Card05BlindSpotViewModel(insightsService),
            new Card06DayOfWeekViewModel(insightsService),
            new Card07NextDayViewModel(insightsService),
            new Card08CombosViewModel(insightsService),
            new Card09TagTrendViewModel(insightsService),
            new Card10InfluenceViewModel(insightsService),
        ];
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var to   = DateTime.UtcNow;
        var from = to.AddDays(-SelectedPeriod.Days);

        IsPageLoading = true;
        try
        {
            // Каждая карточка перехватывает свои ошибки — Task.WhenAll не бросает
            await Task.WhenAll(Cards.Select(c => c.LoadAsync(from, to)));
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
