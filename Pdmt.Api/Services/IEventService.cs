using Pdmt.Api.Dto;

namespace Pdmt.Api.Services;

public interface IEventService
{
    Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(
        Guid userId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DtoEventType? type,
        IReadOnlyList<Guid>? tagIds,
        int? minIntensity,
        int? maxIntensity,
        CancellationToken ct);

    Task<EventResponseDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct);
    Task<EventResponseDto> CreateEventAsync(Guid userId, CreateEventDto ev, CancellationToken ct);
    Task<bool> UpdateEventAsync(Guid userId, Guid id, UpdateEventDto ev, CancellationToken ct);
    Task<bool> DeleteEventAsync(Guid userId, Guid id, CancellationToken ct);
}
