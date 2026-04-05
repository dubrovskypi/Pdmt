using Pdmt.Maui.Models;

namespace Pdmt.Maui.ViewModels;

public class EventItemViewModel(EventResponseDto dto)
{
    public Guid Id => dto.Id;
    public DateTimeOffset Timestamp => dto.Timestamp;
    public EventType Type => dto.Type;
    public int Intensity => dto.Intensity;
    public string Title => dto.Title;
    public bool HasTags => dto.Tags.Count > 0;
    public string TagsSummary => string.Join(", ", dto.Tags.Select(t => t.Name));

    public Color TypeColor => Type == EventType.Positive
        ? Color.FromArgb("#2E7D32")
        : Color.FromArgb("#C62828");

    public string TypeLabel => Type == EventType.Positive ? "+" : "−";
}
