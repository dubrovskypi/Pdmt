using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;
using Pdmt.Maui.ViewModels.Cards;
using System.Collections.ObjectModel;

namespace Pdmt.Maui.ViewModels;

public partial class DotViewModel : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DotColor))]
    [NotifyPropertyChangedFor(nameof(DotWidth))]
    private bool _isSelected;

    public DotViewModel(int index, bool isSelected)
    {
        Index = index;
        IsSelected = isSelected;
    }

    public Color DotColor => IsSelected
        ? (Color)Application.Current!.Resources["Primary"]
        : (Color)Application.Current!.Resources["Border"];

    public double DotWidth => IsSelected ? 16 : 8;
}

public partial class InsightsViewModel : ObservableObject
{
    public record PeriodOption(string Label, int Days);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekSelected))]
    [NotifyPropertyChangedFor(nameof(IsTwoWeeksSelected))]
    [NotifyPropertyChangedFor(nameof(IsMonthSelected))]
    [NotifyPropertyChangedFor(nameof(WeekChipBg))]
    [NotifyPropertyChangedFor(nameof(WeekChipText))]
    [NotifyPropertyChangedFor(nameof(TwoWeeksChipBg))]
    [NotifyPropertyChangedFor(nameof(TwoWeeksChipText))]
    [NotifyPropertyChangedFor(nameof(MonthChipBg))]
    [NotifyPropertyChangedFor(nameof(MonthChipText))]
    private PeriodOption _selectedPeriod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardCounterLabel))]
    private int _currentPosition;

    [ObservableProperty]
    private bool _isPageLoading;

    private CancellationTokenSource? _cts;
    private int _previousDotIndex;
    private bool _isLoaded;

    public event Action<int>? ScrollToCardRequested;

    public IReadOnlyList<PeriodOption> PeriodOptions { get; } = [
        new("Week", 7),
        new("2 weeks", 14),
        new("Month", 30),
    ];

    public ObservableCollection<InsightCardViewModel> Cards { get; }
    public ObservableCollection<DotViewModel> Dots { get; }

    public bool IsWeekSelected => SelectedPeriod == PeriodOptions[0];
    public bool IsTwoWeeksSelected => SelectedPeriod == PeriodOptions[1];
    public bool IsMonthSelected => SelectedPeriod == PeriodOptions[2];

    public string CardCounterLabel => $"{CurrentPosition + 1} / {Cards.Count}";

    public Color WeekChipBg => IsWeekSelected
        ? (Color)Application.Current!.Resources["Primary"] : Colors.Transparent;
    public Color WeekChipText => IsWeekSelected
        ? (Color)Application.Current!.Resources["OnPrimary"]
        : (Color)Application.Current!.Resources["OnSurfaceVariant"];
    public Color TwoWeeksChipBg => IsTwoWeeksSelected
        ? (Color)Application.Current!.Resources["Primary"] : Colors.Transparent;
    public Color TwoWeeksChipText => IsTwoWeeksSelected
        ? (Color)Application.Current!.Resources["OnPrimary"]
        : (Color)Application.Current!.Resources["OnSurfaceVariant"];
    public Color MonthChipBg => IsMonthSelected
        ? (Color)Application.Current!.Resources["Primary"] : Colors.Transparent;
    public Color MonthChipText => IsMonthSelected
        ? (Color)Application.Current!.Resources["OnPrimary"]
        : (Color)Application.Current!.Resources["OnSurfaceVariant"];

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

        Dots = new ObservableCollection<DotViewModel>(
            Cards.Select((_, i) => new DotViewModel(i, i == 0)));
    }

    partial void OnCurrentPositionChanged(int value)
    {
        if (_previousDotIndex < Dots.Count)
            Dots[_previousDotIndex].IsSelected = false;
        if (value < Dots.Count)
            Dots[value].IsSelected = true;
        _previousDotIndex = value;
    }

    public void CancelLoad()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    [RelayCommand]
    private void GoToCard(int index) => ScrollToCardRequested?.Invoke(index);

    public void SetCurrentPosition(int index) => CurrentPosition = index;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_isLoaded) return;
        CancelLoad();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-SelectedPeriod.Days);

        IsPageLoading = true;
        try
        {
            await Task.WhenAll(Cards.Take(2).Select(c => c.LoadAsync(from, to, showLoading: false, ct)));
            IsPageLoading = false;
            await Task.WhenAll(Cards.Skip(2).Select(c => c.LoadAsync(from, to, showLoading: true, ct)));
            if (!ct.IsCancellationRequested)
                _isLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception) when (ct.IsCancellationRequested) { }
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
        _isLoaded = false;
        await LoadAsync();
    }
}
