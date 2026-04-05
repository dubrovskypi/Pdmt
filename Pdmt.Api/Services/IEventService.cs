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
        int? maxIntensity);

    Task<EventResponseDto?> GetByIdAsync(Guid userId, Guid id);
    Task<EventResponseDto> CreateEventAsync(Guid userId, CreateEventDto ev);
    Task<bool> UpdateEventAsync(Guid userId, Guid id, UpdateEventDto ev);
    Task DeleteEventAsync(Guid userId, Guid id);
}
