namespace Pdmt.Client.Models
{
    public class CalendarWeekDto
    {
        public DateTimeOffset WeekStart { get; set; }
        public DateTimeOffset WeekEnd { get; set; }
        public List<CalendarDayDetailsDto> Days { get; set; } = [];
    }

    public class CalendarDayDetailsDto
    {
        public DateTimeOffset Date { get; set; }
        public int PosCount { get; set; }
        public int NegCount { get; set; }
        public int PositiveIntensitySum { get; set; }
        public int NegativeIntensitySum { get; set; }
        public double DayScore { get; set; }
        public List<TagCountDto> TopPositiveTags { get; set; } = [];
        public List<TagCountDto> TopNegativeTags { get; set; } = [];
    }

    public class TagCountDto
    {
        public required string Name { get; set; }
        public int Count { get; set; }
    }
}
