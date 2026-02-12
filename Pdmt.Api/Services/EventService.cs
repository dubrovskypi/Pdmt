using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using System.Linq.Expressions;

namespace Pdmt.Api.Services
{
    public class EventService : IEventService
    {
        private readonly AppDbContext _db;
        public EventService(AppDbContext db)
        {
            _db = db;
        }
        public async Task<EventResponseDto> CreateEventAsync(Guid userId, CreateEventDto ev)
        {
            if (ev is null)
                throw new ArgumentNullException(nameof(ev), "Event cannot be null.");
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            var entity = new Event
            {
                Id = Guid.NewGuid(),
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
            return MapToResponseDto(entity); 
        }

        public async Task DeleteEventAsync(Guid userId, Guid id)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (ev is null) return;
            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
        }

        public async Task<EventResponseDto?> GetByIdAsync(Guid userId, Guid id)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            var ev = await _db.Events.
                AsNoTracking().
                FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            return ev is null ? null : MapToResponseDto(ev);
        }

        public async Task<IEnumerable<EventResponseDto>> GetEventsAsync(Guid userId, DateTime? from, DateTime? to, int? type, string? category, bool? isRelationship, int? minIntensity, int? maxIntensity)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            var query = _db.Events.
                AsNoTracking().
                Where(e => e.UserId == userId);
            if (from.HasValue)
                query = query.Where(e => e.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(e => e.Timestamp <= to.Value);
            if (type.HasValue)
                query = query.Where(e => e.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(e => e.Category == category);
            if (isRelationship.HasValue)
                query = query.Where(e => e.IsRelationship == isRelationship.Value);
            if (minIntensity.HasValue)
                query = query.Where(e => e.Intensity >= minIntensity.Value);
            if (maxIntensity.HasValue)
                query = query.Where(e => e.Intensity <= maxIntensity.Value);
            var eventDtos = await query.Select(Projection).ToListAsync();
            return eventDtos;
        }

        public async Task<bool> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventDto newEvent)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            var existing = await _db.Events.
                FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
            if (existing is null) return false;
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
            return true;
        }

        //FOR DEBUGGING PURPOSES ONLY
        public async Task<IEnumerable<EventResponseDto>> GetAllEventsAsync()
        {
            var query = _db.Events.
                AsNoTracking();
            var eventDtos = await query.Select(Projection).ToListAsync();
            return eventDtos;
        }

        private static EventResponseDto MapToResponseDto(Event entity)
        {
            return new EventResponseDto
            {
                Id = entity.Id,
                Timestamp = entity.Timestamp,
                Type = entity.Type,
                Category = entity.Category,
                Intensity = entity.Intensity,
                Title = entity.Title,
                Description = entity.Description,
                Context = entity.Context,
                CanInfluence = entity.CanInfluence,
                IsRelationship = entity.IsRelationship
            };
        }

        private static readonly Expression<Func<Event, EventResponseDto>> Projection =
            e => new EventResponseDto
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                Type = e.Type,
                Category = e.Category,
                Intensity = e.Intensity,
                Title = e.Title,
                Description = e.Description,
                Context = e.Context,
                CanInfluence = e.CanInfluence,
                IsRelationship = e.IsRelationship
            };
    }
}
