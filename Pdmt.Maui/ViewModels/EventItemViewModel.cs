using Pdmt.Maui.Models;

namespace Pdmt.Maui.ViewModels;

public class EventItemViewModel(EventResponseDto dto)
{
    public Guid Id => dto.Id;
    public DateTime Timestamp => dto.Timestamp;
    public int Type => dto.Type;
    public int Intensity => dto.Intensity;
    public string Title => dto.Title;
    public bool HasTags => dto.Tags.Count > 0;
    public string TagsSummary => string.Join(", ", dto.Tags.Select(t => t.Name));
}
