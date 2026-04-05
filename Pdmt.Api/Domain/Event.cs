namespace Pdmt.Api.Domain;

public class Event
{

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public EventType Type { get; set; }
    public int Intensity { get; set; } // 0-10
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Context { get; set; }
    public bool CanInfluence { get; set; }
    public ICollection<EventTag> EventTags { get; set; } = [];
}
