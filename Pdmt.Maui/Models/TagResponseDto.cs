namespace Pdmt.Maui.Models;

public class TagResponseDto
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int EventCount { get; set; }
}
