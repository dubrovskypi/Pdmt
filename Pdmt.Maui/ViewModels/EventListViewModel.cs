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
        ..Enum.GetValues<EventType>()
        .Select(t => new EventTypeFilter(t.ToString(), t))
        ];

    public ObservableCollection<TagFilter> TagFilters { get; } = [];

    [ObservableProperty]
    private DateTimeOffset? _filterFrom;

    [ObservableProperty]
    private DateTimeOffset? _filterTo;

    [ObservableProperty]
    private EventTypeFilter _selectedTypeFilter = new("All", null);

    [ObservableProperty]
    private TagFilter? _selectedTagFilter;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var tags = await tagService.GetTagsAsync();
            TagFilters.Clear();
            foreach (var tag in tags)
                TagFilters.Add(new TagFilter(tag.Name, tag.Id));

            await ApplyFiltersAsync();
        }
        catch
        {
            ErrorMessage = "Не удалось загрузить события";
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
        try
        {
            await ApplyFiltersAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        var tagIds = SelectedTagFilter?.Id is Guid id
            ? (IReadOnlyList<Guid>)[id]
            : null;

        var results = await eventService.GetEventsAsync(
            FilterFrom, FilterTo, SelectedTypeFilter.Value, tagIds);

        Events.Clear();
        foreach (var e in results.OrderByDescending(e => e.Timestamp))
            Events.Add(new EventItemViewModel(e));
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        FilterFrom = null;
        FilterTo = null;
        SelectedTypeFilter = EventTypeFilters[0];
        SelectedTagFilter = null;
        await ApplyFiltersAsync();
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
            ErrorMessage = "Не удалось удалить событие";
        }
    }

    [RelayCommand]
    private static async Task NavigateToAddAsync() =>
        await Shell.Current.GoToAsync("addEvent");
}
