namespace Pdmt.Client.Models
{
    public class EventResponseDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int Type { get; set; }
        public required string Category { get; set; }
        public int Intensity { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? Context { get; set; }
        public bool CanInfluence { get; set; }
        public bool IsRelationship { get; set; }
    }
}