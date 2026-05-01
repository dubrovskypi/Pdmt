using Pdmt.Maui.Models;

namespace Pdmt.Maui.ViewModels;

public class EventItemViewModel(EventResponseDto dto)
{
    public Guid Id => dto.Id;
    public DateTimeOffset Timestamp => dto.Timestamp;
    public DateTimeOffset LocalTimestamp => dto.Timestamp.ToLocalTime();
    public EventType Type => dto.Type;
    public int Intensity => dto.Intensity;
    public string Title => dto.Title;
    public string? Description => dto.Description;
    public bool HasDescription => dto.Description is not null;
    public string? Context => dto.Context;
    public bool HasContext => dto.Context is not null;
    public bool CanInfluence => dto.CanInfluence;
    public IReadOnlyList<TagResponseDto> Tags => dto.Tags;

    public Color BarColor => Type == EventType.Positive
        ? Color.FromArgb("#4ade80")
        : Color.FromArgb("#f87171");

    public Color MetaColor => Type == EventType.Positive
        ? Color.FromArgb("#16a34a")
        : Color.FromArgb("#dc2626");

    public Color TypeColor => MetaColor;

    public Color CardBackgroundColor => Type == EventType.Positive
        ? Color.FromArgb("#f0fdf4")
        : Color.FromArgb("#fff1f2");

    public Color CardBorderColor => Type == EventType.Positive
        ? Color.FromArgb("#bbf7d0")
        : Color.FromArgb("#fecdd3");
}
