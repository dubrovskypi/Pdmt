using Pdmt.Maui.Models;

namespace Pdmt.Maui.ViewModels;

public class EventItemViewModel(EventResponseDto dto)
{
    public Guid Id => dto.Id;
    public DateTimeOffset Timestamp => dto.Timestamp;
    public EventType Type => dto.Type;
    public int Intensity => dto.Intensity;
    public string Title => dto.Title;
    public string? Description => dto.Description;
    public bool HasDescription => dto.Description is not null;
    public string? Context => dto.Context;
    public bool HasContext => dto.Context is not null;
    public bool CanInfluence => dto.CanInfluence;
    public IReadOnlyList<TagResponseDto> Tags => dto.Tags;

    public Color TypeColor => Type == EventType.Positive
        ? Color.FromArgb("#2E7D32")
        : Color.FromArgb("#C62828");

    public Color CardBackgroundColor => Type == EventType.Positive
        ? Color.FromArgb("#F1F8E9")
        : Color.FromArgb("#FFEBEE");

    public Color CardBorderColor => Type == EventType.Positive
        ? Color.FromArgb("#C8E6C9")
        : Color.FromArgb("#FFCDD2");
}
