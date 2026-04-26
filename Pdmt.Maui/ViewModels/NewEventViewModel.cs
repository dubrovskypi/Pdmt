using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class NewEventViewModel(EventService eventService, TagService tagService)
    : EventFormViewModel(tagService)
{
    [ObservableProperty]
    private string? _context;

    [RelayCommand]
    private async Task LoadTagsAsync() => await LoadTagsInternalAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await eventService.CreateEventAsync(new CreateEventDto
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = IsPositive ? EventType.Positive : EventType.Negative,
                Intensity = Intensity,
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
            ErrorMessage = "Failed to save event. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
