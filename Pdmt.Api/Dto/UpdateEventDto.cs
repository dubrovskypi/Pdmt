using System.ComponentModel.DataAnnotations;

namespace Pdmt.Api.Dto
{
    public class UpdateEventDto
    {
        public DateTimeOffset Timestamp { get; set; }

        public DtoEventType Type { get; set; }

        [Range(0, 10)]
        public int Intensity { get; set; }

        [Required, MaxLength(200)]
        public required string Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Context { get; set; }

        public bool CanInfluence { get; set; }

        public List<string> TagNames { get; set; } = [];
    }
}
