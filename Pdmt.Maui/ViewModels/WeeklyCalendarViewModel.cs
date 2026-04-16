using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class WeeklyCalendarViewModel(
    AnalyticsService analyticsService,
    EventService eventService) : ObservableObject
{
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateTimeOffset _weekStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateTimeOffset _weekEnd;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

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
            // API encodes local dates as UTC midnight (2026-04-14T00:00:00Z = local April 14).
            // SpecifyKind=Local so DateTimeOffset constructor picks up the device timezone offset.
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
            ErrorMessage = "Не удалось загрузить календарь. Попробуйте ещё раз.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTimeOffset GetMonday(DateTimeOffset date)
    {
        // Use local device time to determine correct day of week.
        // Early morning in UTC+ timezones would give wrong week if we used UTC.
        var local = date.ToLocalTime();
        int daysToMonday = ((int)local.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var localMondayDate = local.AddDays(-daysToMonday).Date;
        // API convention: local dates are encoded as UTC midnight (e.g. local Apr 14 → 2026-04-14T00:00:00Z)
        return new DateTimeOffset(localMondayDate, TimeSpan.Zero);
    }
}
