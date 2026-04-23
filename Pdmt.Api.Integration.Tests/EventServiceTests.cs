using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests;

public class EventServiceTests
{
    private readonly AppDbContext _db;
    private readonly EventService _service;

    public EventServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _service = new EventService(_db);
    }

    #region GetEventsAsync

    [Fact]
    public async Task GetEventsAsync_EmptyUserId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEventsAsync(Guid.Empty, null, null, null, null, null, null));
    }

    [Fact]
    public async Task GetEventsAsync_NoFilters_ReturnsAllUserEvents()
    {
        var userId = Guid.NewGuid();
        _db.Events.AddRange(TestHelpers.MakeEvent(userId, "A"), TestHelpers.MakeEvent(userId, "B", EventType.Negative));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEventsAsync_OtherUsersEvents_NotReturned()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _db.Events.AddRange(TestHelpers.MakeEvent(userId, "Mine"), TestHelpers.MakeEvent(otherUserId, "Theirs"));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, null, null);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_NoEvents_ReturnsEmptyList()
    {
        var result = await _service.GetEventsAsync(Guid.NewGuid(), null, null, null, null, null, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByType_ReturnsMatchingEvents()
    {
        var userId = Guid.NewGuid();
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Pos", EventType.Positive, 7),
            TestHelpers.MakeEvent(userId, "Neg", EventType.Negative, 4));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, DtoEventType.Negative, null, null, null);

        Assert.Single(result);
        Assert.Equal("Neg", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByFrom_BoundaryIsInclusive()
    {
        var userId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Before", timestamp: from.AddDays(-1)),
            TestHelpers.MakeEvent(userId, "OnBoundary", timestamp: from),
            TestHelpers.MakeEvent(userId, "After", timestamp: from.AddDays(1)));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, from, null, null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "Before");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByTo_BoundaryIsInclusive()
    {
        var userId = Guid.NewGuid();
        var to = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Before", timestamp: to.AddDays(-1)),
            TestHelpers.MakeEvent(userId, "OnBoundary", timestamp: to),
            TestHelpers.MakeEvent(userId, "After", timestamp: to.AddDays(1)));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, to, null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "After");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByDateRange_BothBoundariesInclusive()
    {
        var userId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 6, 20, 0, 0, 0, TimeSpan.Zero);
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "TooEarly", timestamp: from.AddDays(-1)),
            TestHelpers.MakeEvent(userId, "Start", timestamp: from),
            TestHelpers.MakeEvent(userId, "Middle", timestamp: from.AddDays(5)),
            TestHelpers.MakeEvent(userId, "End", timestamp: to),
            TestHelpers.MakeEvent(userId, "TooLate", timestamp: to.AddDays(1)));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, from, to, null, null, null, null);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "TooEarly");
        Assert.DoesNotContain(result, e => e.Title == "TooLate");
    }

    [Fact]
    public async Task GetEventsAsync_FromEqualsTo_ReturnsSinglePointInTime()
    {
        var userId = Guid.NewGuid();
        var point = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Before", timestamp: point.AddSeconds(-1)),
            TestHelpers.MakeEvent(userId, "Exact", timestamp: point),
            TestHelpers.MakeEvent(userId, "After", timestamp: point.AddSeconds(1)));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, point, point, null, null, null, null);

        Assert.Single(result);
        Assert.Equal("Exact", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByDateRange_WorksWhenEventHasNonUtcOffset()
    {
        var userId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 6, 10, 8, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);

        // 12:00+03:00 = 09:00 UTC — within [08:00, 12:00]
        var insideOffset = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.FromHours(3));
        // 06:00+03:00 = 03:00 UTC — before lower bound
        var beforeOffset = new DateTimeOffset(2024, 6, 10, 6, 0, 0, TimeSpan.FromHours(3));
        // 10:59:59+03:00 = 07:59:59 UTC — one second before lower bound
        var justBeforeFrom = new DateTimeOffset(2024, 6, 10, 10, 59, 59, TimeSpan.FromHours(3));
        // 11:00:01+03:00 = 08:00:01 UTC — one second after lower bound
        var justAfterFrom = new DateTimeOffset(2024, 6, 10, 11, 0, 1, TimeSpan.FromHours(3));
        // 17:00+03:00 = 14:00 UTC — after upper bound
        var afterOffset = new DateTimeOffset(2024, 6, 10, 17, 0, 0, TimeSpan.FromHours(3));

        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Inside", timestamp: insideOffset),
            TestHelpers.MakeEvent(userId, "Before", timestamp: beforeOffset),
            TestHelpers.MakeEvent(userId, "JustBeforeFrom", timestamp: justBeforeFrom),
            TestHelpers.MakeEvent(userId, "JustAfterFrom", timestamp: justAfterFrom),
            TestHelpers.MakeEvent(userId, "After", timestamp: afterOffset));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, from, to, null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Title == "Inside");
        Assert.Contains(result, e => e.Title == "JustAfterFrom");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMinIntensity_ExcludesBelowThreshold()
    {
        var userId = Guid.NewGuid();
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Low", intensity: 3),
            TestHelpers.MakeEvent(userId, "High", intensity: 7));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, 5, null);

        Assert.Single(result);
        Assert.Equal("High", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMaxIntensity_ExcludesAboveThreshold()
    {
        var userId = Guid.NewGuid();
        _db.Events.AddRange(
            TestHelpers.MakeEvent(userId, "Low", intensity: 3),
            TestHelpers.MakeEvent(userId, "High", intensity: 7));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, null, 5);

        Assert.Single(result);
        Assert.Equal("Low", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterBySingleTag_ReturnsOnlyTaggedEvents()
    {
        var userId = Guid.NewGuid();
        var tag = TestHelpers.MakeTag(userId, "Work");
        _db.Tags.Add(tag);

        var eventWithTag = TestHelpers.MakeEvent(userId, "A");
        var eventWithoutTag = TestHelpers.MakeEvent(userId, "B");
        _db.Events.AddRange(eventWithTag, eventWithoutTag);
        _db.EventTags.Add(new EventTag { EventId = eventWithTag.Id, TagId = tag.Id });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, [tag.Id], null, null);

        Assert.Single(result);
        Assert.Equal("A", result[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMultipleTags_ReturnsEventsWithAnyTag()
    {
        var userId = Guid.NewGuid();
        var tagWork = TestHelpers.MakeTag(userId, "Work");
        var tagHealth = TestHelpers.MakeTag(userId, "Health");
        _db.Tags.AddRange(tagWork, tagHealth);

        var evWork = TestHelpers.MakeEvent(userId, "Work event");
        var evHealth = TestHelpers.MakeEvent(userId, "Health event");
        var evNone = TestHelpers.MakeEvent(userId, "No tags");
        _db.Events.AddRange(evWork, evHealth, evNone);
        _db.EventTags.Add(new EventTag { EventId = evWork.Id, TagId = tagWork.Id });
        _db.EventTags.Add(new EventTag { EventId = evHealth.Id, TagId = tagHealth.Id });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, [tagWork.Id, tagHealth.Id], null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Title == "No tags");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_EmptyUserId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetByIdAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_OwnEvent_ReturnsDto()
    {
        var userId = Guid.NewGuid();
        var ts = new DateTimeOffset(2024, 5, 20, 15, 30, 0, TimeSpan.Zero);
        var ev = TestHelpers.MakeEvent(userId, "My Event", intensity: 6, timestamp: ts);
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetByIdAsync(userId, ev.Id);

        Assert.NotNull(result);
        Assert.Equal(ev.Id, result.Id);
        Assert.Equal("My Event", result.Title);
        Assert.Equal(ts, result.Timestamp);
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersEvent_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var ev = TestHelpers.MakeEvent(ownerId, "Test");
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetByIdAsync(Guid.NewGuid(), ev.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentEvent_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    #endregion

    #region CreateEventAsync

    [Fact]
    public async Task CreateEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateEventAsync(Guid.Empty, TestHelpers.MakeCreateDto()));
    }

    [Fact]
    public async Task CreateEventAsync_NullDto_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CreateEventAsync(Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task CreateEventAsync_ValidDto_PersistsWithCorrectFields()
    {
        var userId = Guid.NewGuid();
        var ts = new DateTimeOffset(2024, 4, 10, 9, 0, 0, TimeSpan.Zero);
        var dto = TestHelpers.MakeCreateDto("Promotion", intensity: 8, timestamp: ts, canInfluence: true);

        var result = await _service.CreateEventAsync(userId, dto);

        var entity = await _db.Events.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.Equal(userId, entity.UserId);
        Assert.Equal(dto.Title, entity.Title);
        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(ts, result.Timestamp);
    }

    [Fact]
    public async Task CreateEventAsync_ValidDto_GeneratesNonEmptyId()
    {
        var result = await _service.CreateEventAsync(Guid.NewGuid(), TestHelpers.MakeCreateDto());

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateEventAsync_WithNewTagNames_CreatesAndLinksTags()
    {
        var userId = Guid.NewGuid();
        var dto = TestHelpers.MakeCreateDto(tagNames: ["Work", "Health"]);

        var result = await _service.CreateEventAsync(userId, dto);

        Assert.Equal(2, result.Tags.Count);
        Assert.Contains(result.Tags, t => t.Name == "Work");
        Assert.Contains(result.Tags, t => t.Name == "Health");
        Assert.Equal(2, await _db.Tags.CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateEventAsync_ExistingTagName_ReusesTag()
    {
        var userId = Guid.NewGuid();
        var existingTag = TestHelpers.MakeTag(userId, "Work");
        _db.Tags.Add(existingTag);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.CreateEventAsync(userId, TestHelpers.MakeCreateDto(tagNames: ["Work"]));

        Assert.Single(result.Tags);
        Assert.Equal(existingTag.Id, result.Tags[0].Id);
        Assert.Equal(1, await _db.Tags.CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateEventAsync_EmptyTagNames_NoTagsCreated()
    {
        var result = await _service.CreateEventAsync(Guid.NewGuid(), TestHelpers.MakeCreateDto(tagNames: []));

        Assert.Empty(result.Tags);
        Assert.Equal(0, await _db.Tags.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateEventAsync_TagNameExistsForOtherUser_CreatesNewTag()
    {
        var userId = Guid.NewGuid();
        var otherTag = TestHelpers.MakeTag(Guid.NewGuid(), "Work");
        _db.Tags.Add(otherTag);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.CreateEventAsync(userId, TestHelpers.MakeCreateDto(tagNames: ["Work"]));

        Assert.Single(result.Tags);
        Assert.NotEqual(otherTag.Id, result.Tags[0].Id);
        Assert.Equal(2, await _db.Tags.CountAsync(t => t.Name == "Work", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateEventAsync_TagNamesWithWhitespace_AreTrimmed()
    {
        var result = await _service.CreateEventAsync(Guid.NewGuid(), TestHelpers.MakeCreateDto(tagNames: ["  Work  "]));

        Assert.Single(result.Tags);
        Assert.Equal("Work", result.Tags[0].Name);
    }

    [Fact]
    public async Task CreateEventAsync_DuplicateTagNames_CreatesOnlyOneTag()
    {
        var userId = Guid.NewGuid();
        var result = await _service.CreateEventAsync(userId, TestHelpers.MakeCreateDto(tagNames: ["Work", "Work"]));

        Assert.Single(result.Tags);
        Assert.Equal(1, await _db.Tags.CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateEventAsync_IntensityZero_SavesSuccessfully()
    {
        var result = await _service.CreateEventAsync(Guid.NewGuid(), TestHelpers.MakeCreateDto(intensity: 0));

        Assert.Equal(0, result.Intensity);
    }

    [Fact]
    public async Task CreateEventAsync_IntensityTen_SavesSuccessfully()
    {
        var result = await _service.CreateEventAsync(Guid.NewGuid(), TestHelpers.MakeCreateDto(intensity: 10));

        Assert.Equal(10, result.Intensity);
    }

    #endregion

    #region UpdateEventAsync

    [Fact]
    public async Task UpdateEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "T", Intensity = 5 };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateEventAsync(Guid.Empty, Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task UpdateEventAsync_ValidDto_UpdatesAllFields()
    {
        var userId = Guid.NewGuid();
        var newTs = new DateTimeOffset(2024, 8, 1, 18, 0, 0, TimeSpan.Zero);
        var ev = TestHelpers.MakeEvent(userId, "Old Title", EventType.Negative, 3,
            new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero));
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = new UpdateEventDto { Timestamp = newTs, Type = DtoEventType.Positive, Title = "New Title", Intensity = 9 };
        var result = await _service.UpdateEventAsync(userId, ev.Id, dto);

        var updated = await _db.Events.FirstAsync(TestContext.Current.CancellationToken);
        Assert.True(result);
        Assert.Equal(EventType.Positive, updated.Type);
        Assert.Equal("New Title", updated.Title);
        Assert.Equal(9, updated.Intensity);
        Assert.Equal(newTs, updated.Timestamp);
    }

    [Fact]
    public async Task UpdateEventAsync_NonExistentEvent_ReturnsFalse()
    {
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Updated", Intensity = 5 };

        var result = await _service.UpdateEventAsync(Guid.NewGuid(), Guid.NewGuid(), dto);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateEventAsync_OtherUsersEvent_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var ev = TestHelpers.MakeEvent(ownerId, "T");
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Hacked", Intensity = 5 };
        var result = await _service.UpdateEventAsync(Guid.NewGuid(), ev.Id, dto);

        Assert.False(result);
        var unchanged = await _db.Events.FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("T", unchanged.Title);
    }

    [Fact]
    public async Task UpdateEventAsync_WithNewTagName_AddsTag()
    {
        var userId = Guid.NewGuid();
        var ev = TestHelpers.MakeEvent(userId, "T");
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateEventAsync(userId, ev.Id, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work"]
        });

        Assert.Equal(1, await _db.EventTags.CountAsync(et => et.EventId == ev.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateEventAsync_EmptyTagNames_RemovesAllTags()
    {
        var userId = Guid.NewGuid();
        var tag = TestHelpers.MakeTag(userId, "Work");
        var ev = TestHelpers.MakeEvent(userId, "T");
        _db.Tags.Add(tag);
        _db.Events.Add(ev);
        _db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateEventAsync(userId, ev.Id, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = []
        });

        Assert.Equal(0, await _db.EventTags.CountAsync(et => et.EventId == ev.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateEventAsync_ReplacesExistingTagWithNewTag()
    {
        var userId = Guid.NewGuid();
        var tagWork = TestHelpers.MakeTag(userId, "Work");
        var ev = TestHelpers.MakeEvent(userId, "T");
        _db.Tags.Add(tagWork);
        _db.Events.Add(ev);
        _db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tagWork.Id });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateEventAsync(userId, ev.Id, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Health"]
        });

        var eventTags = await _db.EventTags.Where(et => et.EventId == ev.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(eventTags);
        var tag = await _db.Tags.FindAsync([eventTags[0].TagId, TestContext.Current.CancellationToken], TestContext.Current.CancellationToken);
        Assert.Equal("Health", tag!.Name);
    }

    #endregion

    #region DeleteEventAsync

    [Fact]
    public async Task DeleteEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DeleteEventAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteEventAsync_ExistingEvent_RemovesIt()
    {
        var userId = Guid.NewGuid();
        var ev = TestHelpers.MakeEvent(userId, "Test");
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.DeleteEventAsync(userId, ev.Id);

        Assert.False(await _db.Events.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteEventAsync_NonExistentEvent_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => _service.DeleteEventAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteEventAsync_OtherUsersEvent_DoesNotDelete()
    {
        var ownerId = Guid.NewGuid();
        var ev = TestHelpers.MakeEvent(ownerId, "T");
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.DeleteEventAsync(Guid.NewGuid(), ev.Id);

        Assert.True(await _db.Events.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteEventAsync_EventWithTags_RemovesEventTags()
    {
        var userId = Guid.NewGuid();
        var tag = TestHelpers.MakeTag(userId, "Work");
        var ev = TestHelpers.MakeEvent(userId, "T");
        _db.Tags.Add(tag);
        _db.Events.Add(ev);
        _db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.DeleteEventAsync(userId, ev.Id);

        Assert.False(await _db.Events.AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await _db.EventTags.AnyAsync(et => et.EventId == ev.Id, TestContext.Current.CancellationToken));
    }

    #endregion
}
