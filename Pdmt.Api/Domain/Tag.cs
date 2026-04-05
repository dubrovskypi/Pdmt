namespace Pdmt.Api.Domain;

public class Tag
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<EventTag> EventTags { get; set; } = [];
}
