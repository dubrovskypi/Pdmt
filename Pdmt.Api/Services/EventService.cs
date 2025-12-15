using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public class EventService : IEventService
    {
        private readonly AppDbContext _db;
        public EventService(AppDbContext db)
        {
            _db = db;
        }
        public async Task CreateEventAsync(Guid userId, EventDto ev)
        {
            if (ev is null)
                throw new ArgumentNullException(nameof(ev), "Event cannot be null.");
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            var entity = new Event
            {
                Id = ev.Id,
                UserId = userId,
                Timestamp = ev.Timestamp,
                Type = ev.Type,
                Category = ev.Category,
                Intensity = ev.Intensity,
                Title = ev.Title,
                Description = ev.Description,
                Context = ev.Context,
                CanInfluence = ev.CanInfluence,
                IsRelationship = ev.IsRelationship
            };
            _db.Events.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteEventAsync(Guid userId, Guid id)
        {
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (ev is null) return;
            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
        }

        public async Task<EventDto?> GetByIdAsync(Guid userId, Guid id)
        {
            //todo use userId to restrict access
            var ev = await _db.Events.
                AsNoTracking().
                FirstOrDefaultAsync(e => e.Id == id);
            if (ev is null) return null;
            var evDto = new EventDto
            {
                Id = ev.Id,
                Timestamp = ev.Timestamp,
                Type = ev.Type,
                Category = ev.Category,
                Intensity = ev.Intensity,
                Title = ev.Title,
                Description = ev.Description,
                Context = ev.Context,
                CanInfluence = ev.CanInfluence,
                IsRelationship = ev.IsRelationship
            };
            return evDto;
        }

        public async Task<IEnumerable<EventDto>> GetEventsAsync(Guid userId, DateTime? from, DateTime? to, int? type, string? category, bool? isRelationship, int? minIntensity, int? maxIntensity)
        {
            var query = _db.Events.
                AsNoTracking().
                Where(e => e.UserId == userId);
            if (from.HasValue)
                query = query.Where(e => e.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(e => e.Timestamp <= to.Value);
            if (type.HasValue)
                query = query.Where(e => e.Type == type.Value);
            if (!string.IsNullOrEmpty(category))
                query = query.Where(e => e.Category == category);
            if (isRelationship.HasValue)
                query = query.Where(e => e.IsRelationship == isRelationship.Value);
            if (minIntensity.HasValue)
                query = query.Where(e => e.Intensity >= minIntensity.Value);
            if (maxIntensity.HasValue)
                query = query.Where(e => e.Intensity <= maxIntensity.Value);
            var eventDtos = await query.
                Select(ev => new EventDto
            {
                Id = ev.Id,
                Timestamp = ev.Timestamp,
                Type = ev.Type,
                Category = ev.Category,
                Intensity = ev.Intensity,
                Title = ev.Title,
                Description = ev.Description,
                Context = ev.Context,
                CanInfluence = ev.CanInfluence,
                IsRelationship = ev.IsRelationship
            }).ToListAsync();
            return eventDtos;
        }

        public async Task UpdateEventAsync(Guid userId, Guid eventId, EventDto newEvent)
        {
            //todo use userId to restrict access
            var existing = await _db.Events.
                FirstOrDefaultAsync(e => e.Id == eventId);
            if (existing is null) return;
            existing.Timestamp = newEvent.Timestamp;
            existing.Type = newEvent.Type;
            existing.Category = newEvent.Category;
            existing.Intensity = newEvent.Intensity;
            existing.Title = newEvent.Title;
            existing.Description = newEvent.Description;
            existing.Context = newEvent.Context;
            existing.CanInfluence = newEvent.CanInfluence;
            existing.IsRelationship = newEvent.IsRelationship;
            await _db.SaveChangesAsync();
        }

        //FOR DEBUGGING PURPOSES ONLY
        public async Task<IEnumerable<EventDto>> GetAllEventsAsync()
        {
            var query = _db.Events.
                AsNoTracking();
            var eventDtos = await query.
                Select(ev => new EventDto
                {
                    Id = ev.Id,
                    Timestamp = ev.Timestamp,
                    Type = ev.Type,
                    Category = ev.Category,
                    Intensity = ev.Intensity,
                    Title = ev.Title,
                    Description = ev.Description,
                    Context = ev.Context,
                    CanInfluence = ev.CanInfluence,
                    IsRelationship = ev.IsRelationship
                }).ToListAsync();
            return eventDtos;
        }
    }
}
