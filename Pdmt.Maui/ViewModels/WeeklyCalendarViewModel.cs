using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class WeeklyCalendarViewModel(
    AnalyticsService analyticsService,
    EventService eventService) : ObservableObject
{
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    [NotifyPropertyChangedFor(nameof(IsCurrentWeek))]
    private DateTime _weekStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateTime _weekEnd;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public bool IsCurrentWeek => WeekStart >= GetMonday(DateTime.UtcNow.Date);
    public string WeekLabel => $"{WeekStart:dd MMM} \u2014 {WeekEnd:dd MMM yyyy}";

    [RelayCommand]
    private async Task LoadAsync()
    {
        WeekStart = GetMonday(DateTime.UtcNow.Date);
        WeekEnd = WeekStart.AddDays(6);
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task PrevWeekAsync()
    {
        WeekStart = WeekStart.AddDays(-7);
        WeekEnd = WeekStart.AddDays(6);
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        if (IsCurrentWeek) return;
        WeekStart = WeekStart.AddDays(7);
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

        // Collapse any other expanded day
        foreach (var d in Days.Where(d => d.IsExpanded))
        {
            d.IsExpanded = false;
            d.ExpandedEvents.Clear();
        }

        day.IsExpanded = true;
        day.IsExpandedLoading = true;
        try
        {
            var events = await eventService.GetEventsAsync(
                from: DateTime.SpecifyKind(day.Date, DateTimeKind.Utc),
                to: DateTime.SpecifyKind(day.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));

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
            if (week is not null)
            {
                double maxSum = week.Days.Count > 0
                    ? week.Days.Max(d => (double)Math.Max(d.PositiveIntensitySum, d.NegativeIntensitySum))
                    : 0;

                foreach (var day in week.Days)
                    Days.Add(new CalendarDayViewModel(day, maxSum));
            }
        }
        catch
        {
            ErrorMessage = "Не удалось загрузить календарь. Попробуйте ещё раз.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}
