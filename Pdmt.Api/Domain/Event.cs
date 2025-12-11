namespace Pdmt.Api.Domain;

public class Event
{

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public int Type { get; set; } // 0=Negative, 1=Positive
    public string Category { get; set; }
    public int Intensity { get; set; } // 0-10
    public string Title { get; set; }
    public string? Description { get; set; }
    public string? Context { get; set; } // Work/Home/Street/etc
    public bool CanInfluence { get; set; }
    public bool IsRelationship { get; set; }
}
