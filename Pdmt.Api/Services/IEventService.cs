using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IEventService
    {
        Task<IEnumerable<EventDto>> GetEventsAsync(DateTime? from, DateTime? to, int? type, string? category, bool? isRelationship, int? minIntensity, int? maxIntensity);
        Task<EventDto?> GetByIdAsync(Guid id);
        Task CreateEventAsync(EventDto ev);
        Task UpdateEventAsync(EventDto ev);
        Task DeleteEventAsync(Guid id);
    }
}