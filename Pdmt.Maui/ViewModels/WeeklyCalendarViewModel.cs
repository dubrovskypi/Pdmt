using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public enum CalendarViewMode { Week, Month }

public partial class WeeklyCalendarViewModel(
    AnalyticsService analyticsService,
    EventService eventService) : ObservableObject
{
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];
    public ObservableCollection<MonthDayCellViewModel> MonthDays { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    [NotifyPropertyChangedFor(nameof(PeriodLabel))]
    private DateTimeOffset _weekStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateTimeOffset _weekEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekMode))]
    [NotifyPropertyChangedFor(nameof(IsMonthMode))]
    [NotifyPropertyChangedFor(nameof(PeriodLabel))]
    [NotifyPropertyChangedFor(nameof(WeekModeBg))]
    [NotifyPropertyChangedFor(nameof(WeekModeText))]
    [NotifyPropertyChangedFor(nameof(MonthModeBg))]
    [NotifyPropertyChangedFor(nameof(MonthModeText))]
    private CalendarViewMode _viewMode = CalendarViewMode.Week;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodLabel))]
    private DateTime _monthAnchor = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private int _monthPosTotal;
    [ObservableProperty] private int _monthNegTotal;
    [ObservableProperty] private string _monthBestDayLabel = "—";
    [ObservableProperty] private string _monthWorstDayLabel = "—";
    [ObservableProperty] private string _monthSummaryHeader = "";

    public string WeekLabel => $"{WeekStart:dd MMM} — {WeekEnd:dd MMM}";

    public string PeriodLabel => IsWeekMode
        ? $"{WeekStart:dd MMM} — {WeekEnd:dd MMM}"
        : MonthAnchor.ToString("MMMM yyyy");

    public bool IsWeekMode => ViewMode == CalendarViewMode.Week;
    public bool IsMonthMode => ViewMode == CalendarViewMode.Month;

    public Color WeekModeBg => IsWeekMode
        ? (Color)Application.Current!.Resources["Primary"]
        : Colors.Transparent;

    public Color WeekModeText => IsWeekMode
        ? (Color)Application.Current!.Resources["OnPrimary"]
        : (Color)Application.Current!.Resources["OnSurfaceVariant"];

    public Color MonthModeBg => IsMonthMode
        ? (Color)Application.Current!.Resources["Primary"]
        : Colors.Transparent;

    public Color MonthModeText => IsMonthMode
        ? (Color)Application.Current!.Resources["OnPrimary"]
        : (Color)Application.Current!.Resources["OnSurfaceVariant"];

    [RelayCommand]
    private async Task SetViewModeAsync(string mode)
    {
        var newMode = mode == "month" ? CalendarViewMode.Month : CalendarViewMode.Week;
        if (ViewMode == newMode) return;
        ViewMode = newMode;
        if (IsMonthMode && MonthDays.Count == 0)
            await LoadMonthAsync();
    }

    [RelayCommand]
    private async Task PrevPeriodAsync()
    {
        if (IsWeekMode)
        {
            WeekStart = WeekStart.AddDays(-7);
            WeekEnd = WeekStart.AddDays(6);
            await LoadWeekAsync();
        }
        else
        {
            MonthAnchor = MonthAnchor.AddMonths(-1);
            await LoadMonthAsync();
        }
    }

    [RelayCommand]
    private async Task NextPeriodAsync()
    {
        if (IsWeekMode)
        {
            WeekStart = WeekStart.AddDays(7);
            WeekEnd = WeekStart.AddDays(6);
            await LoadWeekAsync();
        }
        else
        {
            MonthAnchor = MonthAnchor.AddMonths(1);
            await LoadMonthAsync();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        WeekStart = GetMonday(DateTimeOffset.UtcNow);
        WeekEnd = WeekStart.AddDays(6);
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task ToggleDayAsync(CalendarDayViewModel day)
    {
        if (day.IsExpanded)
        {
            day.IsExpanded = false;
            day.ExpandedEvents.Clear();
            return;
        }

        foreach (var d in Days.Where(d => d.IsExpanded))
        {
            d.IsExpanded = false;
            d.ExpandedEvents.Clear();
        }

        day.IsExpanded = true;
        day.IsExpandedLoading = true;
        try
        {
            var localDate = DateTime.SpecifyKind(day.Date.UtcDateTime.Date, DateTimeKind.Local);
            var dayStart = new DateTimeOffset(localDate);
            var dayEnd = dayStart.AddDays(1).AddTicks(-1);
            var events = await eventService.GetEventsAsync(from: dayStart, to: dayEnd);

            day.ExpandedEvents.Clear();
            foreach (var ev in events.OrderBy(e => e.Timestamp))
                day.ExpandedEvents.Add(new CalendarEventViewModel(ev));
        }
        catch
        {
            // Leave ExpandedEvents empty — XAML shows "no events" state
        }
        finally
        {
            day.IsExpandedLoading = false;
            day.IsExpandedEmpty = day.ExpandedEvents.Count == 0;
        }
    }

    private async Task LoadWeekAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        Days.Clear();
        try
        {
            var week = await analyticsService.GetCalendarWeekAsync(WeekStart);

            var dayDict = week?.Days.ToDictionary(d => d.Date.Date) ?? [];
            double maxSum = week?.Days.Count > 0
                ? week.Days.Max(d => (double)Math.Max(d.PositiveIntensitySum, d.NegativeIntensitySum))
                : 0;

            for (int i = 0; i < 7; i++)
            {
                var date = WeekStart.AddDays(i);
                var dto = dayDict.GetValueOrDefault(date.Date, new CalendarDayDetailsDto { Date = date });
                Days.Add(new CalendarDayViewModel(dto, maxSum));
            }
        }
        catch
        {
            ErrorMessage = "Failed to load calendar. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadMonthAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        MonthDays.Clear();
        try
        {
            var monthDto = await analyticsService.GetCalendarMonthAsync(MonthAnchor.Year, MonthAnchor.Month);
            var days = monthDto?.Days ?? [];

            double maxAbsScore = days.Count > 0 ? days.Max(d => Math.Abs(d.DayScore)) : 0;

            // Leading placeholder cells (Mon = 0, Tue = 1, ...)
            var firstOfMonth = new DateTime(MonthAnchor.Year, MonthAnchor.Month, 1);
            int leadingBlanks = ((int)firstOfMonth.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            for (int i = 0; i < leadingBlanks; i++)
                MonthDays.Add(new MonthDayCellViewModel(default, 0, 0, 0, 0, isPlaceholder: true));

            var dayDict = days.ToDictionary(d => d.Date.UtcDateTime.Date);
            int daysInMonth = DateTime.DaysInMonth(MonthAnchor.Year, MonthAnchor.Month);
            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(MonthAnchor.Year, MonthAnchor.Month, d);
                dayDict.TryGetValue(date, out var dto);
                MonthDays.Add(new MonthDayCellViewModel(
                    date,
                    dto?.PosCount ?? 0,
                    dto?.NegCount ?? 0,
                    dto?.DayScore ?? 0,
                    maxAbsScore));
            }

            // Summary
            MonthPosTotal = days.Sum(d => d.PosCount);
            MonthNegTotal = days.Sum(d => d.NegCount);
            MonthSummaryHeader = MonthAnchor.ToString("MMMM").ToUpperInvariant() + " SUMMARY";

            var best = days.Where(d => d.DayScore > 0).MaxBy(d => d.DayScore);
            MonthBestDayLabel = best is not null
                ? $"{best.Date.UtcDateTime:MMM d} (+{best.DayScore:F1})"
                : "—";

            var worst = days.Where(d => d.DayScore < 0).MinBy(d => d.DayScore);
            MonthWorstDayLabel = worst is not null
                ? $"{worst.Date.UtcDateTime:MMM d} ({worst.DayScore:F1})"
                : "—";
        }
        catch
        {
            ErrorMessage = "Failed to load calendar. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTimeOffset GetMonday(DateTimeOffset date)
    {
        var local = date.ToLocalTime();
        int daysToMonday = ((int)local.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var localMondayDate = local.AddDays(-daysToMonday).Date;
        return new DateTimeOffset(localMondayDate, TimeSpan.Zero);
    }
}
