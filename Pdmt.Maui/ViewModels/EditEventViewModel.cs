using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Models;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class EditEventViewModel(EventService eventService, TagService tagService)
    : EventFormViewModel(tagService), IQueryAttributable
{
    private Guid _id;

    [ObservableProperty]
    private string? _context;

    [ObservableProperty]
    private DateTime _eventDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _eventTime = DateTime.Now.TimeOfDay;

    private DateTimeOffset EventTimestamp =>
        new(DateTime.SpecifyKind(EventDate.Date + EventTime, DateTimeKind.Utc));

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
            await LoadTagsInternalAsync();

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

            var local = ev.Timestamp.LocalDateTime;
            EventDate = local.Date;
            EventTime = local.TimeOfDay;

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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await eventService.UpdateEventAsync(_id, new UpdateEventDto
            {
                Timestamp = EventTimestamp,
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
            ErrorMessage = "Failed to save changes. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
