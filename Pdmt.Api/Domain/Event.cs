namespace Pdmt.Api.Domain;

public class Event
{

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public int Type { get; set; } // 0=Negative, 1=Positive
    public int Intensity { get; set; } // 0-10
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Context { get; set; }
    public bool CanInfluence { get; set; }
    public List<EventTag> EventTags { get; set; } = new();
}
