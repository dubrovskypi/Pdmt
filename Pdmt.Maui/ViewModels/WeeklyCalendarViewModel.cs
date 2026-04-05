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
    private DateTimeOffset _weekStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateTimeOffset _weekEnd;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public bool IsCurrentWeek => WeekStart >= GetMonday(DateTimeOffset.UtcNow);
    public string WeekLabel => $"{WeekStart:dd MMM} \u2014 {WeekEnd:dd MMM yyyy}";

    [RelayCommand]
    private async Task LoadAsync()
    {
        WeekStart = GetMonday(DateTimeOffset.UtcNow);
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
            var dateTime = day.Date.Date;
            var dayStart = new DateTimeOffset(dateTime, TimeSpan.Zero);
            var dayEnd = new DateTimeOffset(dateTime.AddDays(1).AddTicks(-1), TimeSpan.Zero);
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

    private static DateTimeOffset StartOfDayUtc(DateTimeOffset date)
    {
        return new DateTimeOffset(date.UtcDateTime.Date, TimeSpan.Zero);
    }

    private static DateTimeOffset GetMonday(DateTimeOffset date)
    {
        var utcDate = date.ToUniversalTime();
        int daysToMonday = ((int)utcDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var mondayDate = utcDate.AddDays(-daysToMonday).Date;
        return new DateTimeOffset(mondayDate, TimeSpan.Zero);
    }
}
