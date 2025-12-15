using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IEventService
    {
        Task<IEnumerable<EventDto>> GetEventsAsync(Guid userId, DateTime? from, DateTime? to, int? type, string? category, bool? isRelationship, int? minIntensity, int? maxIntensity);
        Task<EventDto?> GetByIdAsync(Guid userId, Guid id);
        Task CreateEventAsync(Guid userId, EventDto ev);
        Task UpdateEventAsync(Guid userId, Guid id, EventDto ev);
        Task DeleteEventAsync(Guid userId, Guid id);
        //FOR DEBUGGING PURPOSES ONLY
        Task<IEnumerable<EventDto>> GetAllEventsAsync();
    }
}