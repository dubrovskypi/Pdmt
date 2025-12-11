using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public class EventService : IEventService
    {
        Task IEventService.CreateEventAsync(EventDto ev)
        {
            throw new NotImplementedException();
        }

        Task IEventService.DeleteEventAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        Task<EventDto?> IEventService.GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<EventDto>> IEventService.GetEventsAsync(DateTime? from, DateTime? to, int? type, string? category, bool? isRelationship, int? minIntensity, int? maxIntensity)
        {
            throw new NotImplementedException();
        }

        Task IEventService.UpdateEventAsync(EventDto ev)
        {
            throw new NotImplementedException();
        }
    }
}
