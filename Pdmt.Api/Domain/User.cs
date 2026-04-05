namespace Pdmt.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<Event> Events { get; set; } = [];
    public ICollection<Summary> Summaries { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
}
