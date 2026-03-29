using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class EventListViewModel(
    EventService eventService,
    TagService tagService,
    AuthService authService,
    ITokenService tokenService) : ObservableObject
{
    public record EventTypeFilter(string Label, int? Value);
    public record TagFilter(string Name, Guid? Id);

    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    public IReadOnlyList<EventTypeFilter> EventTypeFilters { get; } = [
        new("Все", null),
        new("Положительные", 1),
        new("Отрицательные", 0),
    ];

    public ObservableCollection<TagFilter> TagFilters { get; } = [];

    [ObservableProperty]
    private DateTime? _filterFrom;

    [ObservableProperty]
    private DateTime? _filterTo;

    [ObservableProperty]
    private EventTypeFilter _selectedTypeFilter = new("Все", null);

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

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try { await authService.LogoutAsync(); }
        catch { /* Ignore — clear tokens anyway */ }

        await tokenService.ClearAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
