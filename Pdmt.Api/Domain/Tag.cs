namespace Pdmt.Api.Domain;

public class Tag
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<EventTag> EventTags { get; set; } = new();
}
