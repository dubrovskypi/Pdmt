using Pdmt.Api.Domain;

namespace Pdmt.Api.Integration.Tests.Infrastructure.Builders;

public sealed class TagBuilder
{
    private Guid _userId = TestUserHelper.TestUserId;
    private string _name = "test-tag";

    public TagBuilder WithUserId(Guid userId) { _userId = userId; return this; }
    public TagBuilder WithName(string name) { _name = name; return this; }

    public Tag Build() => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        Name = _name,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
