namespace Pdmt.Api.Dto
{
    public class EventResponseDto
    {
        public Guid Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public DtoEventType Type { get; set; }
        public int Intensity { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? Context { get; set; }
        public bool CanInfluence { get; set; }
        public IReadOnlyList<TagResponseDto> Tags { get; set; } = [];
    }
}
