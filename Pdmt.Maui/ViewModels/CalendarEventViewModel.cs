using Pdmt.Maui.Models;

namespace Pdmt.Maui.ViewModels;

public class CalendarEventViewModel(EventResponseDto dto)
{
    public EventType Type => dto.Type;
    public string Title => dto.Title;
    public string? Description => dto.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(dto.Description);
    public int Intensity => dto.Intensity;
    public string IntensityText => $"{dto.Intensity}/10";
    public string TimeText => dto.Timestamp.ToLocalTime().ToString("HH:mm");
    public bool HasTags => dto.Tags.Count > 0;
    public string TagsSummary => string.Join(", ", dto.Tags.Select(t => t.Name));

    public Color TypeColor => Type == EventType.Positive
        ? Color.FromArgb("#2E7D32")
        : Color.FromArgb("#C62828");

    public string TypeLabel => Type == EventType.Positive ? "+" : "−";
}
