using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;
using System.Collections.ObjectModel;

namespace Pdmt.Maui.ViewModels;

public partial class EventListViewModel(
    EventService eventService,
    TagService tagService) : ObservableObject
{
    public record EventTypeFilter(string Label, EventType? Value);
    public record TagFilter(string Name, Guid? Id);

    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    public IReadOnlyList<EventTypeFilter> EventTypeFilters { get; } = [
        new("All", null),
        new("Positive", EventType.Positive),
        new("Negative", EventType.Negative),
    ];

    public ObservableCollection<TagFilter> TagFilters { get; } = [];

    private bool _filtersInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private DateTime? _filterFrom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private DateTime? _filterTo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private EventTypeFilter _selectedTypeFilter = new("All", null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private TagFilter? _selectedTagFilter;

    [ObservableProperty]
    private bool _isFilterPanelVisible;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasActiveFilters =>
        SelectedTypeFilter.Value is not null
        || SelectedTagFilter is not null
        || (FilterFrom.HasValue && FilterFrom.Value.Date != DateTime.Today.AddDays(-6))
        || (FilterTo.HasValue && FilterTo.Value.Date != DateTime.Today);

    public bool IsAllTypeSelected => SelectedTypeFilter.Value is null;
    public bool IsPositiveTypeSelected => SelectedTypeFilter.Value == EventType.Positive;
    public bool IsNegativeTypeSelected => SelectedTypeFilter.Value == EventType.Negative;

    [RelayCommand]
    private void SelectTypeFilter(EventTypeFilter filter) => SelectedTypeFilter = filter;

    partial void OnSelectedTypeFilterChanged(EventTypeFilter value)
    {
        OnPropertyChanged(nameof(IsAllTypeSelected));
        OnPropertyChanged(nameof(IsPositiveTypeSelected));
        OnPropertyChanged(nameof(IsNegativeTypeSelected));
        if (_filtersInitialized) _ = ApplyFiltersAsync();
    }

    partial void OnSelectedTagFilterChanged(TagFilter? value)
    {
        if (_filtersInitialized) _ = ApplyFiltersAsync();
    }

    partial void OnFilterFromChanged(DateTime? value)
    {
        if (_filtersInitialized) _ = ApplyFiltersAsync();
    }

    partial void OnFilterToChanged(DateTime? value)
    {
        if (_filtersInitialized) _ = ApplyFiltersAsync();
    }

    [RelayCommand]
    private void ToggleFilterPanel() => IsFilterPanelVisible = !IsFilterPanelVisible;

    private void SetDefaultDateRange()
    {
        FilterFrom = DateTime.Today.AddDays(-6);
        FilterTo = DateTime.Today;
    }

    private async Task FetchAndPopulateAsync()
    {
        var tagIds = SelectedTagFilter?.Id is Guid id
            ? (IReadOnlyList<Guid>)[id]
            : null;

        DateTimeOffset? fromOffset = FilterFrom.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(FilterFrom.Value.Date, DateTimeKind.Local))
                .ToUniversalTime()
            : null;
        DateTimeOffset? toOffset = FilterTo.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(FilterTo.Value.Date, DateTimeKind.Local))
                .AddDays(1).AddMilliseconds(-1).ToUniversalTime()
            : null;

        var results = await eventService.GetEventsAsync(
            fromOffset, toOffset, SelectedTypeFilter.Value, tagIds);

        Events.Clear();
        foreach (var e in results.OrderByDescending(e => e.Timestamp))
            Events.Add(new EventItemViewModel(e));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (!FilterFrom.HasValue || !FilterTo.HasValue)
                SetDefaultDateRange();

            if (TagFilters.Count == 0)
            {
                var tags = await tagService.GetTagsAsync();
                TagFilters.Clear();
                foreach (var tag in tags)
                    TagFilters.Add(new TagFilter(tag.Name, tag.Id));
            }

            await FetchAndPopulateAsync();
            _filtersInitialized = true;
        }
        catch
        {
            ErrorMessage = "Failed to load events";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        ErrorMessage = null;
        try
        {
            await FetchAndPopulateAsync();
        }
        catch
        {
            ErrorMessage = "Failed to refresh events";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await FetchAndPopulateAsync();
        }
        catch
        {
            ErrorMessage = "Failed to load events";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        _filtersInitialized = false;
        SetDefaultDateRange();
        SelectedTypeFilter = EventTypeFilters[0];
        SelectedTagFilter = null;
        _filtersInitialized = true;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await FetchAndPopulateAsync();
        }
        catch
        {
            ErrorMessage = "Failed to load events";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteEventAsync(Guid id)
    {
        try
        {
            await eventService.DeleteEventAsync(id);
            var item = Events.FirstOrDefault(e => e.Id == id);
            if (item is not null)
                Events.Remove(item);
        }
        catch
        {
            ErrorMessage = "Failed to delete event";
        }
    }

    [RelayCommand]
    private static async Task NavigateToAddAsync() =>
        await Shell.Current.GoToAsync("addEvent");

    [RelayCommand]
    private static async Task NavigateToEditAsync(Guid id) =>
        await Shell.Current.GoToAsync($"editEvent?id={id}");
}
