using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;

namespace Pdmt.Api.Services;

public class EventService(AppDbContext db) : IEventService
{
    public async Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(
        Guid userId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DtoEventType? type,
        IReadOnlyList<Guid>? tagIds,
        int? minIntensity,
        int? maxIntensity,
        CancellationToken ct)
    {
        var query = db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        if (type.HasValue)
            query = query.Where(e => e.Type == (EventType)type.Value);
        if (tagIds is not null && tagIds.Count > 0)
            query = query.Where(e => e.EventTags.Any(et => tagIds.Contains(et.TagId)));
        if (minIntensity.HasValue)
            query = query.Where(e => e.Intensity >= minIntensity.Value);
        if (maxIntensity.HasValue)
            query = query.Where(e => e.Intensity <= maxIntensity.Value);

        var events = await query
            .Include(e => e.EventTags)
            .ThenInclude(et => et.Tag)
            .ToListAsync(ct);

        return events.Select(MapToResponseDto).ToList();
    }

    public async Task<EventResponseDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ev = await db.Events
            .AsNoTracking()
            .Include(e => e.EventTags)
            .ThenInclude(et => et.Tag)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        return ev is null ? null : MapToResponseDto(ev);
    }

    public async Task<EventResponseDto> CreateEventAsync(Guid userId, CreateEventDto ev, CancellationToken ct)
    {
        var eventId = Guid.NewGuid();
        var resolvedTags = await ResolveTagsAsync(userId, ev.TagNames, ct);
        var entity = new Event
        {
            Id = eventId,
            UserId = userId,
            Timestamp = ev.Timestamp,
            Type = (EventType)ev.Type,
            Intensity = ev.Intensity,
            Title = ev.Title,
            Description = ev.Description,
            Context = ev.Context,
            CanInfluence = ev.CanInfluence,
            EventTags = resolvedTags
                .Select(t => new EventTag { EventId = eventId, TagId = t.Id, Tag = t })
                .ToList()
        };

        db.Events.Add(entity);
        await db.SaveChangesAsync(ct);

        return MapToResponseDto(entity);
    }

    public async Task<bool> UpdateEventAsync(Guid userId, Guid id, UpdateEventDto ev, CancellationToken ct)
    {
        var existing = await db.Events
            .Include(e => e.EventTags)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (existing is null) return false;

        existing.Timestamp = ev.Timestamp;
        existing.Type = (EventType)ev.Type;
        existing.Intensity = ev.Intensity;
        existing.Title = ev.Title;
        existing.Description = ev.Description;
        existing.Context = ev.Context;
        existing.CanInfluence = ev.CanInfluence;

        var resolvedTags = await ResolveTagsAsync(userId, ev.TagNames, ct);
        var newTagIds = resolvedTags.Select(t => t.Id).ToHashSet();
        var oldTagIds = existing.EventTags.Select(et => et.TagId).ToHashSet();

        db.EventTags.RemoveRange(existing.EventTags.Where(et => !newTagIds.Contains(et.TagId)));
        db.EventTags.AddRange(resolvedTags
            .Where(t => !oldTagIds.Contains(t.Id))
            .Select(t => new EventTag { EventId = existing.Id, TagId = t.Id }));

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteEventAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (ev is null) return;
        db.Events.Remove(ev);
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<Tag>> ResolveTagsAsync(Guid userId, List<string> tagNames, CancellationToken ct)
    {
        if (tagNames.Count == 0) return new();

        var normalized = tagNames
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();

        var existing = await db.Tags
            .Where(t => t.UserId == userId && normalized.Contains(t.Name))
            .ToListAsync(ct);

        var existingNames = existing.Select(t => t.Name).ToHashSet();

        var newTags = normalized
            .Where(n => !existingNames.Contains(n))
            .Select(n => new Tag
            {
                Id = Guid.NewGuid(),
                Name = n,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        if (newTags.Count > 0)
            db.Tags.AddRange(newTags);

        return existing.Concat(newTags).ToList();
    }

    private static EventResponseDto MapToResponseDto(Event entity) =>
        new()
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            Type = (DtoEventType)entity.Type,
            Intensity = entity.Intensity,
            Title = entity.Title,
            Description = entity.Description,
            Context = entity.Context,
            CanInfluence = entity.CanInfluence,
            Tags = entity.EventTags
                .Select(et => new TagResponseDto { Id = et.Tag.Id, Name = et.Tag.Name })
                .ToList()
        };
}
