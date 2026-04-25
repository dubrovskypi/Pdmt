using Pdmt.Api.Domain;
using Pdmt.Api.Dto;

namespace Pdmt.Api.Integration.Tests;

internal static class TestHelpers
{
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
