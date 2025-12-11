namespace Pdmt.Api.Dto
{
    public class EventDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int Type { get; set; }
        public string Category { get; set; }
        public int Intensity { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Context { get; set; }
        public bool CanInfluence { get; set; }
        public bool IsRelationship { get; set; }
    }
}
