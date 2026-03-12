using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IEventService
    {
        Task<IEnumerable<EventResponseDto>> GetEventsAsync(Guid userId, DateTime? from, DateTime? to, int? type, string? category, bool? isRelationship, int? minIntensity, int? maxIntensity);
        Task<EventResponseDto?> GetByIdAsync(Guid userId, Guid id);
        Task<EventResponseDto> CreateEventAsync(Guid userId, CreateEventDto ev);
        Task<bool> UpdateEventAsync(Guid userId, Guid id, UpdateEventDto ev);
        Task DeleteEventAsync(Guid userId, Guid id);
    }
}