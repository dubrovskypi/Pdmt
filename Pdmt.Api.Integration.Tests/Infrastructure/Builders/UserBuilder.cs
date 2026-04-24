using Pdmt.Api.Domain;

namespace Pdmt.Api.Integration.Tests.Infrastructure.Builders;

public sealed class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = "user@pdmt.dev";
    private string _password = "Password123!";

    public UserBuilder WithId(Guid id) { _id = id; return this; }
    public UserBuilder WithEmail(string email) { _email = email; return this; }
    public UserBuilder WithPassword(string password) { _password = password; return this; }

    public User Build() => new()
    {
        Id = _id,
        Email = _email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(_password),
        CreatedAt = DateTimeOffset.UtcNow
    };
}
