namespace Pdmt.Client.Models
{
    public class TagResponseDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int EventCount { get; set; }
    }
}
