namespace Pdmt.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Event> Events { get; set; } = new();
    public List<Summary> Summaries { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}
