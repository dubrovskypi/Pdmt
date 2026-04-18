using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class EditEventViewModel(EventService eventService, TagService tagService)
    : ObservableObject, IQueryAttributable
{
    private Guid _id;
    private List<string> _allTagNames = [];

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _isPositive = true;

    [ObservableProperty]
    private double _intensity = 5;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _context;

    [ObservableProperty]
    private bool _canInfluence;

    [ObservableProperty]
    private string _tagInput = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<string> SelectedTags { get; } = [];
    public ObservableCollection<string> TagSuggestions { get; } = [];
    public bool HasSuggestions => TagSuggestions.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id))
            _id = id;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var tags = await tagService.GetTagsAsync();
            _allTagNames = tags.Select(t => t.Name).ToList();

            var ev = await eventService.GetEventAsync(_id);
            if (ev is null)
            {
                ErrorMessage = "Event not found";
                return;
            }

            Title = ev.Title;
            IsPositive = ev.Type == EventType.Positive;
            Intensity = ev.Intensity;
            Description = ev.Description;
            Context = ev.Context;
            CanInfluence = ev.CanInfluence;

            SelectedTags.Clear();
            foreach (var tag in ev.Tags)
                SelectedTags.Add(tag.Name);
        }
        catch
        {
            ErrorMessage = "Failed to load event";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnTagInputChanged(string value)
    {
        TagSuggestions.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            OnPropertyChanged(nameof(HasSuggestions));
            return;
        }

        var matches = _allTagNames
            .Where(n => n.Contains(value, StringComparison.OrdinalIgnoreCase)
                        && !SelectedTags.Contains(n))
            .Take(6);

        foreach (var match in matches)
            TagSuggestions.Add(match);

        OnPropertyChanged(nameof(HasSuggestions));
    }

    [RelayCommand]
    private void SelectSuggestion(string name)
    {
        if (!SelectedTags.Contains(name))
            SelectedTags.Add(name);
        TagInput = "";
        TagSuggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    [RelayCommand]
    private void CommitTagInput()
    {
        var name = TagInput.Trim();
        if (!string.IsNullOrWhiteSpace(name) && !SelectedTags.Contains(name))
            SelectedTags.Add(name);
        TagInput = "";
        TagSuggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    [RelayCommand]
    private void RemoveTag(string name) => SelectedTags.Remove(name);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title is required";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await eventService.UpdateEventAsync(_id, new UpdateEventDto
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = IsPositive ? EventType.Positive : EventType.Negative,
                Intensity = (int)Intensity,
                Title = Title.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                Context = string.IsNullOrWhiteSpace(Context) ? null : Context.Trim(),
                CanInfluence = CanInfluence,
                TagNames = [.. SelectedTags]
            });

            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            ErrorMessage = "Failed to save changes. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
