using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Tests;

public class EventServiceTests
{
    private AppDbContext CreateDbContext() =>
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    #region GetEventsAsync

    [Fact]
    public async Task GetEventsAsync_EmptyUserId_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new EventService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetEventsAsync(Guid.Empty, null, null, null, null, null, null));
    }

    [Fact]
    public async Task GetEventsAsync_NoFilters_ReturnsAllUserEvents()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "A", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Negative, Title = "B", Intensity = 3 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, null, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEventsAsync_OtherUsersEvents_NotReturned()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId,      Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Mine",  Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = otherUserId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Theirs", Intensity = 5 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, null, null, null, null);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_NoEvents_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        var result = await service.GetEventsAsync(userId, null, null, null, null, null, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByType_ReturnsMatchingEvents()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Pos", Intensity = 7 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Negative, Title = "Neg", Intensity = 4 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, DtoEventType.Negative, null, null, null);

        Assert.Single(result);
        Assert.Equal("Neg", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByFrom_BoundaryIsInclusive()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = from.AddDays(-1), Type = EventType.Positive, Title = "Before",     Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = from,             Type = EventType.Positive, Title = "OnBoundary", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = from.AddDays(1),  Type = EventType.Positive, Title = "After",      Intensity = 5 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, from, null, null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "Before");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByTo_BoundaryIsInclusive()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var to = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = to.AddDays(-1), Type = EventType.Positive, Title = "Before",     Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = to,             Type = EventType.Positive, Title = "OnBoundary", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = to.AddDays(1),  Type = EventType.Positive, Title = "After",      Intensity = 5 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, to, null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "After");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByDateRange_BothBoundariesInclusive()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2024, 6, 20, 0, 0, 0, TimeSpan.Zero);

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = from.AddDays(-1), Type = EventType.Positive, Title = "TooEarly", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = from,             Type = EventType.Positive, Title = "Start",    Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = from.AddDays(5),  Type = EventType.Positive, Title = "Middle",   Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = to,               Type = EventType.Positive, Title = "End",      Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = to.AddDays(1),    Type = EventType.Positive, Title = "TooLate",  Intensity = 5 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, from, to, null, null, null, null);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "TooEarly");
        Assert.DoesNotContain(result, e => e.Title == "TooLate");
    }

    [Fact]
    public async Task GetEventsAsync_FromEqualsTo_ReturnsSinglePointInTime()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var point = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = point.AddSeconds(-1), Type = EventType.Positive, Title = "Before", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = point,                Type = EventType.Positive, Title = "Exact",  Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = point.AddSeconds(1),  Type = EventType.Positive, Title = "After",  Intensity = 5 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, point, point, null, null, null, null);

        Assert.Single(result);
        Assert.Equal("Exact", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByDateRange_WorksWhenEventHasNonUtcOffset()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        // Границы фильтра в UTC
        var from = new DateTimeOffset(2024, 6, 10,  8, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);

        // 12:00+03:00 = 09:00 UTC — попадает в [08:00, 12:00] UTC
        var insideOffset   = new DateTimeOffset(2024, 6, 10, 12,  0,  0, TimeSpan.FromHours(3));
        // 06:00+03:00 = 03:00 UTC — до нижней границы
        var beforeOffset   = new DateTimeOffset(2024, 6, 10,  6,  0,  0, TimeSpan.FromHours(3));
        // 10:59:59+03:00 = 07:59:59 UTC — секунда до нижней границы
        var justBeforeFrom = new DateTimeOffset(2024, 6, 10, 10, 59, 59, TimeSpan.FromHours(3));
        // 11:00:01+03:00 = 08:00:01 UTC — секунда после нижней границы, попадает
        var justAfterFrom  = new DateTimeOffset(2024, 6, 10, 11,  0,  1, TimeSpan.FromHours(3));
        // 17:00+03:00 = 14:00 UTC — после верхней границы
        var afterOffset    = new DateTimeOffset(2024, 6, 10, 17,  0,  0, TimeSpan.FromHours(3));

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = insideOffset,   Type = EventType.Positive, Title = "Inside",        Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = beforeOffset,   Type = EventType.Positive, Title = "Before",        Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = justBeforeFrom, Type = EventType.Positive, Title = "JustBeforeFrom", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = justAfterFrom,  Type = EventType.Positive, Title = "JustAfterFrom", Intensity = 5 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = afterOffset,    Type = EventType.Positive, Title = "After",         Intensity = 5 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, from, to, null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Title == "Inside");
        Assert.Contains(result, e => e.Title == "JustAfterFrom");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMinIntensity_ExcludesBelowThreshold()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Low",  Intensity = 3 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "High", Intensity = 7 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, null, null, 5, null);

        Assert.Single(result);
        Assert.Equal("High", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMaxIntensity_ExcludesAboveThreshold()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Low",  Intensity = 3 },
            new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "High", Intensity = 7 }
        );
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, null, null, null, 5);

        Assert.Single(result);
        Assert.Equal("Low", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterBySingleTag_ReturnsOnlyTaggedEvents()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(tag);

        var eventWithTag    = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "A", Intensity = 5 };
        var eventWithoutTag = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "B", Intensity = 5 };
        db.Events.AddRange(eventWithTag, eventWithoutTag);
        db.EventTags.Add(new EventTag { EventId = eventWithTag.Id, TagId = tag.Id });
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, null, [tag.Id], null, null);

        Assert.Single(result);
        Assert.Equal("A", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMultipleTags_ReturnsEventsWithAnyTag()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();

        var tagWork   = new Tag { Id = Guid.NewGuid(), Name = "Work",   UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        var tagHealth = new Tag { Id = Guid.NewGuid(), Name = "Health", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.AddRange(tagWork, tagHealth);

        var evWork   = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Work event",   Intensity = 5 };
        var evHealth = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Health event", Intensity = 5 };
        var evNone   = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "No tags",      Intensity = 5 };
        db.Events.AddRange(evWork, evHealth, evNone);
        db.EventTags.Add(new EventTag { EventId = evWork.Id,   TagId = tagWork.Id });
        db.EventTags.Add(new EventTag { EventId = evHealth.Id, TagId = tagHealth.Id });
        await db.SaveChangesAsync();

        var result = await service.GetEventsAsync(userId, null, null, null, [tagWork.Id, tagHealth.Id], null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "No tags");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_EmptyUserId_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new EventService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetByIdAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_OwnEvent_ReturnsDto()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ts = new DateTimeOffset(2024, 5, 20, 15, 30, 0, TimeSpan.Zero);
        db.Events.Add(new Event
        {
            Id = eventId,
            UserId = userId,
            Timestamp = ts,
            Type = EventType.Positive,
            Title = "My Event",
            Intensity = 6
        });
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(userId, eventId);

        Assert.NotNull(result);
        Assert.Equal(eventId, result.Id);
        Assert.Equal("My Event", result.Title);
        Assert.Equal(ts, result.Timestamp);
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersEvent_ReturnsNull()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entity = new Event
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Timestamp = DateTimeOffset.UtcNow,
            Type = EventType.Positive,
            Title = "Test",
            Intensity = 5
        };
        db.Events.Add(entity);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(otherUserId, entity.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentEvent_ReturnsNull()
    {
        var db = CreateDbContext();
        var service = new EventService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    #endregion

    #region CreateEventAsync

    [Fact]
    public async Task CreateEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var dto = new CreateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "T", Intensity = 5 };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateEventAsync(Guid.Empty, dto));
    }

    [Fact]
    public async Task CreateEventAsync_NullDto_ThrowsArgumentNullException()
    {
        var db = CreateDbContext();
        var service = new EventService(db);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateEventAsync(Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task CreateEventAsync_ValidDto_PersistsWithCorrectFields()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var ts = new DateTimeOffset(2024, 4, 10, 9, 0, 0, TimeSpan.Zero);
        var dto = new CreateEventDto
        {
            Timestamp = ts,
            Type = DtoEventType.Positive,
            Title = "Promotion",
            Intensity = 8,
            CanInfluence = true
        };

        var result = await service.CreateEventAsync(userId, dto);

        var entity = await db.Events.FirstOrDefaultAsync();
        Assert.NotNull(entity);
        Assert.Equal(userId, entity.UserId);
        Assert.Equal(dto.Title, entity.Title);
        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(ts, result.Timestamp);
    }

    [Fact]
    public async Task CreateEventAsync_ValidDto_GeneratesNonEmptyId()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "Test",
            Intensity = 5
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateEventAsync_WithNewTagNames_CreatesAndLinksTags()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work", "Health"]
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.Equal(2, result.Tags.Count);
        Assert.Contains(result.Tags, t => t.Name == "Work");
        Assert.Contains(result.Tags, t => t.Name == "Health");
        Assert.Equal(2, await db.Tags.CountAsync(t => t.UserId == userId));
    }

    [Fact]
    public async Task CreateEventAsync_ExistingTagName_ReusesTag()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var existingTag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(existingTag);
        await db.SaveChangesAsync();

        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work"]
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.Single(result.Tags);
        Assert.Equal(existingTag.Id, result.Tags[0].Id);
        Assert.Equal(1, await db.Tags.CountAsync(t => t.UserId == userId));
    }

    [Fact]
    public async Task CreateEventAsync_EmptyTagNames_NoTagsCreated()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = []
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.Empty(result.Tags);
        Assert.Equal(0, await db.Tags.CountAsync());
    }

    [Fact]
    public async Task CreateEventAsync_TagNameExistsForOtherUser_CreatesNewTag()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var otherTag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = otherUserId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(otherTag);
        await db.SaveChangesAsync();

        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work"]
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.Single(result.Tags);
        Assert.NotEqual(otherTag.Id, result.Tags[0].Id);
        Assert.Equal(2, await db.Tags.CountAsync(t => t.Name == "Work"));
    }

    [Fact]
    public async Task CreateEventAsync_TagNamesWithWhitespace_AreTrimmed()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["  Work  "]
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.Single(result.Tags);
        Assert.Equal("Work", result.Tags[0].Name);
    }

    [Fact]
    public async Task CreateEventAsync_DuplicateTagNames_CreatesOnlyOneTag()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work", "Work"]
        };

        var result = await service.CreateEventAsync(userId, dto);

        Assert.Single(result.Tags);
        Assert.Equal(1, await db.Tags.CountAsync(t => t.UserId == userId));
    }

    #endregion

    #region UpdateEventAsync

    [Fact]
    public async Task UpdateEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "T", Intensity = 5 };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateEventAsync(Guid.Empty, Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task UpdateEventAsync_ValidDto_UpdatesAllFields()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var newTs = new DateTimeOffset(2024, 8, 1, 18, 0, 0, TimeSpan.Zero);
        db.Events.Add(new Event
        {
            Id = eventId,
            UserId = userId,
            Timestamp = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
            Type = EventType.Negative,
            Title = "Old Title",
            Intensity = 3
        });
        await db.SaveChangesAsync();
        var dto = new UpdateEventDto
        {
            Timestamp = newTs,
            Type = DtoEventType.Positive,
            Title = "New Title",
            Intensity = 9
        };

        var result = await service.UpdateEventAsync(userId, eventId, dto);

        var updated = await db.Events.FirstAsync();
        Assert.True(result);
        Assert.Equal(EventType.Positive, updated.Type);
        Assert.Equal("New Title", updated.Title);
        Assert.Equal(9, updated.Intensity);
        Assert.Equal(newTs, updated.Timestamp);
    }

    [Fact]
    public async Task UpdateEventAsync_NonExistentEvent_ReturnsFalse()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Updated", Intensity = 5 };

        var result = await service.UpdateEventAsync(Guid.NewGuid(), Guid.NewGuid(), dto);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateEventAsync_OtherUsersEvent_ReturnsFalse()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        db.Events.Add(new Event { Id = eventId, UserId = ownerId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "T", Intensity = 5 });
        await db.SaveChangesAsync();
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Hacked", Intensity = 5 };

        var result = await service.UpdateEventAsync(otherUserId, eventId, dto);

        Assert.False(result);
        var unchanged = await db.Events.FirstAsync();
        Assert.Equal("T", unchanged.Title);
    }

    [Fact]
    public async Task UpdateEventAsync_WithNewTagName_AddsTag()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        db.Events.Add(new Event { Id = eventId, UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "T", Intensity = 5 });
        await db.SaveChangesAsync();

        await service.UpdateEventAsync(userId, eventId, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work"]
        });

        Assert.Equal(1, await db.EventTags.CountAsync(et => et.EventId == eventId));
    }

    [Fact]
    public async Task UpdateEventAsync_EmptyTagNames_RemovesAllTags()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(tag);
        var ev = new Event { Id = eventId, UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "T", Intensity = 5 };
        db.Events.Add(ev);
        db.EventTags.Add(new EventTag { EventId = eventId, TagId = tag.Id });
        await db.SaveChangesAsync();

        await service.UpdateEventAsync(userId, eventId, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = []
        });

        Assert.Equal(0, await db.EventTags.CountAsync(et => et.EventId == eventId));
    }

    [Fact]
    public async Task UpdateEventAsync_ReplacesExistingTagWithNewTag()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var tagWork = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(tagWork);
        db.Events.Add(new Event { Id = eventId, UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "T", Intensity = 5 });
        db.EventTags.Add(new EventTag { EventId = eventId, TagId = tagWork.Id });
        await db.SaveChangesAsync();

        await service.UpdateEventAsync(userId, eventId, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Health"]
        });

        var eventTags = await db.EventTags.Where(et => et.EventId == eventId).ToListAsync();
        Assert.Single(eventTags);
        var tag = await db.Tags.FindAsync(eventTags[0].TagId);
        Assert.Equal("Health", tag!.Name);
    }

    #endregion

    #region DeleteEventAsync

    [Fact]
    public async Task DeleteEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new EventService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DeleteEventAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteEventAsync_ExistingEvent_RemovesIt()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        db.Events.Add(new Event { Id = eventId, UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "Test", Intensity = 5 });
        await db.SaveChangesAsync();

        await service.DeleteEventAsync(userId, eventId);

        Assert.False(await db.Events.AnyAsync());
    }

    [Fact]
    public async Task DeleteEventAsync_NonExistentEvent_DoesNotThrow()
    {
        var db = CreateDbContext();
        var service = new EventService(db);

        var exception = await Record.ExceptionAsync(() => service.DeleteEventAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteEventAsync_OtherUsersEvent_DoesNotDelete()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        db.Events.Add(new Event { Id = eventId, UserId = ownerId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "T", Intensity = 5 });
        await db.SaveChangesAsync();

        await service.DeleteEventAsync(otherUserId, eventId);

        Assert.True(await db.Events.AnyAsync());
    }

    [Fact]
    public async Task DeleteEventAsync_EventWithTags_RemovesEventTags()
    {
        var db = CreateDbContext();
        var service = new EventService(db);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Tags.Add(tag);
        db.Events.Add(new Event { Id = eventId, UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "T", Intensity = 5 });
        db.EventTags.Add(new EventTag { EventId = eventId, TagId = tag.Id });
        await db.SaveChangesAsync();

        await service.DeleteEventAsync(userId, eventId);

        Assert.False(await db.Events.AnyAsync());
        Assert.False(await db.EventTags.AnyAsync(et => et.EventId == eventId));
    }

    #endregion
}
