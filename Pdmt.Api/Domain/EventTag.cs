namespace Pdmt.Api.Domain;

public class EventTag
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
