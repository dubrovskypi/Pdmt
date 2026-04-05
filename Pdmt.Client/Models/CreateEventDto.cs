namespace Pdmt.Client.Models
{
    public class CreateEventDto
    {
        public DateTimeOffset Timestamp { get; set; }
        public DtoEventType Type { get; set; }
        public int Intensity { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? Context { get; set; }
        public bool CanInfluence { get; set; }
        public List<string> TagNames { get; set; } = [];
    }
}
