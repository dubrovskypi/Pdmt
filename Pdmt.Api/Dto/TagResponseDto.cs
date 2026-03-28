namespace Pdmt.Api.Dto;

public class TagResponseDto
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
