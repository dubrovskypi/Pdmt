using Pdmt.Api.Domain;
using Pdmt.Api.Dto;

namespace Pdmt.Api.Integration.Tests;

internal static class TestHelpers
{
    internal static Event MakeEvent(
        Guid userId,
        string title = "Test",
        EventType type = EventType.Positive,
        int intensity = 5,
        DateTimeOffset? timestamp = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        Type = type,
        Title = title,
        Intensity = intensity
    };

    internal static Tag MakeTag(Guid userId, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        UserId = userId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    internal static CreateEventDto MakeCreateDto(
        string title = "Test",
        DtoEventType type = DtoEventType.Positive,
        int intensity = 5,
        DateTimeOffset? timestamp = null,
        List<string>? tagNames = null,
        bool canInfluence = false) => new()
    {
        Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        Type = type,
        Title = title,
        Intensity = intensity,
        TagNames = tagNames ?? [],
        CanInfluence = canInfluence
    };
}
