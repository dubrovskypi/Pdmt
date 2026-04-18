using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class NewEventViewModel(EventService eventService, TagService tagService) : ObservableObject
{
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

    [RelayCommand]
    private async Task LoadTagsAsync()
    {
        var tags = await tagService.GetTagsAsync();
        _allTagNames = tags.Select(t => t.Name).ToList();
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
            await eventService.CreateEventAsync(new CreateEventDto
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = IsPositive ? EventType.Positive : EventType.Negative,
                Intensity = (int)Intensity,
                Title = Title.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                CanInfluence = CanInfluence,
                TagNames = [.. SelectedTags]
            });

            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            ErrorMessage = "Failed to save event. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
