namespace Pdmt.Api.Domain;

public class Summary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int TensionLevel { get; set; } // 0-10
    public bool WasAnythingPositive { get; set; }
    public string? MainDrain { get; set; }
}