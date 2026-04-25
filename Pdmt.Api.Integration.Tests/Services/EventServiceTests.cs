using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests.Services;

public class EventServiceTests : ServiceTestBase
{
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private EventService _service = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Db.Users.Add(new UserBuilder().WithId(OtherUserId).WithEmail("other@pdmt.dev").Build());
        await Db.SaveChangesAsync();
        _service = new EventService(Db);
    }

    #region GetEventsAsync

    [Fact]
    public async Task GetEventsAsync_EmptyUserId_ThrowsArgumentException()
    {
        var act = () => _service.GetEventsAsync(Guid.Empty, null, null, null, null, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetEventsAsync_NoFilters_ReturnsAllUserEvents()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("A").Build(),
            new EventBuilder().WithUserId(userId).WithTitle("B").WithType(EventType.Negative).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, null, null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEventsAsync_OtherUsersEvents_NotReturned()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Mine").Build(),
            new EventBuilder().WithUserId(OtherUserId).WithTitle("Theirs").Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, null, null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Mine");
    }

    [Fact]
    public async Task GetEventsAsync_NoEvents_ReturnsEmptyList()
    {
        var result = await _service.GetEventsAsync(TestAuthHandler.TestUserId, null, null, null, null, null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_FilterByType_ReturnsMatchingEvents()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Pos").WithIntensity(7).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("Neg").WithType(EventType.Negative).WithIntensity(4).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, DtoEventType.Negative, null, null, null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Neg");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByFrom_BoundaryIsInclusive()
    {
        var userId = TestAuthHandler.TestUserId;
        var from = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Before").WithTimestamp(from.AddDays(-1)).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("OnBoundary").WithTimestamp(from).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("After").WithTimestamp(from.AddDays(1)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, from, null, null, null, null, null);

        result.Should().HaveCount(2);
        result.Should().NotContain(e => e.Title == "Before");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByTo_BoundaryIsInclusive()
    {
        var userId = TestAuthHandler.TestUserId;
        var to = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Before").WithTimestamp(to.AddDays(-1)).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("OnBoundary").WithTimestamp(to).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("After").WithTimestamp(to.AddDays(1)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, to, null, null, null, null);

        result.Should().HaveCount(2);
        result.Should().NotContain(e => e.Title == "After");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByDateRange_BothBoundariesInclusive()
    {
        var userId = TestAuthHandler.TestUserId;
        var from = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 6, 20, 0, 0, 0, TimeSpan.Zero);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("TooEarly").WithTimestamp(from.AddDays(-1)).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("Start").WithTimestamp(from).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("Middle").WithTimestamp(from.AddDays(5)).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("End").WithTimestamp(to).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("TooLate").WithTimestamp(to.AddDays(1)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, from, to, null, null, null, null);

        result.Should().HaveCount(3);
        result.Should().NotContain(e => e.Title == "TooEarly");
        result.Should().NotContain(e => e.Title == "TooLate");
    }

    [Fact]
    public async Task GetEventsAsync_FromEqualsTo_ReturnsSinglePointInTime()
    {
        var userId = TestAuthHandler.TestUserId;
        var point = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Before").WithTimestamp(point.AddSeconds(-1)).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("Exact").WithTimestamp(point).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("After").WithTimestamp(point.AddSeconds(1)).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, point, point, null, null, null, null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Exact");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByDateRange_WorksWhenEventHasNonUtcOffset()
    {
        var userId = TestAuthHandler.TestUserId;
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

        // PostgreSQL stores timestamptz in UTC — convert before seeding to avoid Npgsql offset rejection
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Inside").WithTimestamp(insideOffset.ToUniversalTime()).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("Before").WithTimestamp(beforeOffset.ToUniversalTime()).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("JustBeforeFrom").WithTimestamp(justBeforeFrom.ToUniversalTime()).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("JustAfterFrom").WithTimestamp(justAfterFrom.ToUniversalTime()).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("After").WithTimestamp(afterOffset.ToUniversalTime()).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, from, to, null, null, null, null);

        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Title == "Inside");
        result.Should().Contain(e => e.Title == "JustAfterFrom");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMinIntensity_ExcludesBelowThreshold()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Low").WithIntensity(3).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("High").WithIntensity(7).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, 5, null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("High");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMaxIntensity_ExcludesAboveThreshold()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Events.AddRange(
            new EventBuilder().WithUserId(userId).WithTitle("Low").WithIntensity(3).Build(),
            new EventBuilder().WithUserId(userId).WithTitle("High").WithIntensity(7).Build());
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, null, null, 5);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Low");
    }

    [Fact]
    public async Task GetEventsAsync_FilterBySingleTag_ReturnsOnlyTaggedEvents()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new TagBuilder().WithUserId(userId).WithName("Work").Build();
        Db.Tags.Add(tag);

        var eventWithTag = new EventBuilder().WithUserId(userId).WithTitle("A").Build();
        var eventWithoutTag = new EventBuilder().WithUserId(userId).WithTitle("B").Build();
        Db.Events.AddRange(eventWithTag, eventWithoutTag);
        Db.EventTags.Add(new EventTag { EventId = eventWithTag.Id, TagId = tag.Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, [tag.Id], null, null);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("A");
    }

    [Fact]
    public async Task GetEventsAsync_FilterByMultipleTags_ReturnsEventsWithAnyTag()
    {
        var userId = TestAuthHandler.TestUserId;
        var tagWork = new TagBuilder().WithUserId(userId).WithName("Work").Build();
        var tagHealth = new TagBuilder().WithUserId(userId).WithName("Health").Build();
        Db.Tags.AddRange(tagWork, tagHealth);

        var evWork = new EventBuilder().WithUserId(userId).WithTitle("Work event").Build();
        var evHealth = new EventBuilder().WithUserId(userId).WithTitle("Health event").Build();
        var evNone = new EventBuilder().WithUserId(userId).WithTitle("No tags").Build();
        Db.Events.AddRange(evWork, evHealth, evNone);
        Db.EventTags.Add(new EventTag { EventId = evWork.Id, TagId = tagWork.Id });
        Db.EventTags.Add(new EventTag { EventId = evHealth.Id, TagId = tagHealth.Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEventsAsync(userId, null, null, null, [tagWork.Id, tagHealth.Id], null, null);

        result.Should().HaveCount(2);
        result.Should().NotContain(e => e.Title == "No tags");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_EmptyUserId_ThrowsArgumentException()
    {
        var act = () => _service.GetByIdAsync(Guid.Empty, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_OwnEvent_ReturnsDto()
    {
        var userId = TestAuthHandler.TestUserId;
        var ts = new DateTimeOffset(2024, 5, 20, 15, 30, 0, TimeSpan.Zero);
        var ev = new EventBuilder().WithUserId(userId).WithTitle("My Event").WithIntensity(6).WithTimestamp(ts).Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetByIdAsync(userId, ev.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(ev.Id);
        result.Title.Should().Be("My Event");
        result.Timestamp.Should().Be(ts);
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersEvent_ReturnsNull()
    {
        var ev = new EventBuilder().WithTitle("Test").Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetByIdAsync(Guid.NewGuid(), ev.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentEvent_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(TestAuthHandler.TestUserId, Guid.NewGuid());

        result.Should().BeNull();
    }

    #endregion

    #region CreateEventAsync

    [Fact]
    public async Task CreateEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var act = () => _service.CreateEventAsync(Guid.Empty, TestHelpers.MakeCreateDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateEventAsync_NullDto_ThrowsArgumentNullException()
    {
        var act = () => _service.CreateEventAsync(Guid.NewGuid(), null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEventAsync_ValidDto_PersistsWithCorrectFields()
    {
        var userId = TestAuthHandler.TestUserId;
        var ts = new DateTimeOffset(2024, 4, 10, 9, 0, 0, TimeSpan.Zero);
        var dto = TestHelpers.MakeCreateDto("Promotion", intensity: 8, timestamp: ts, canInfluence: true);

        var result = await _service.CreateEventAsync(userId, dto);

        var entity = await Db.Events.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        entity.Should().NotBeNull();
        entity!.UserId.Should().Be(userId);
        entity.Title.Should().Be(dto.Title);
        result.Id.Should().Be(entity.Id);
        result.Timestamp.Should().Be(ts);
    }

    [Fact]
    public async Task CreateEventAsync_ValidDto_GeneratesNonEmptyId()
    {
        var result = await _service.CreateEventAsync(TestAuthHandler.TestUserId, TestHelpers.MakeCreateDto());

        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEventAsync_WithNewTagNames_CreatesAndLinksTags()
    {
        var userId = TestAuthHandler.TestUserId;
        var dto = TestHelpers.MakeCreateDto(tagNames: ["Work", "Health"]);

        var result = await _service.CreateEventAsync(userId, dto);

        result.Tags.Should().HaveCount(2);
        result.Tags.Should().Contain(t => t.Name == "Work");
        result.Tags.Should().Contain(t => t.Name == "Health");
        (await Db.Tags.CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task CreateEventAsync_ExistingTagName_ReusesTag()
    {
        var userId = TestAuthHandler.TestUserId;
        var existingTag = new TagBuilder().WithUserId(userId).WithName("Work").Build();
        Db.Tags.Add(existingTag);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.CreateEventAsync(userId, TestHelpers.MakeCreateDto(tagNames: ["Work"]));

        result.Tags.Should().ContainSingle();
        result.Tags[0].Id.Should().Be(existingTag.Id);
        (await Db.Tags.CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task CreateEventAsync_EmptyTagNames_NoTagsCreated()
    {
        var result = await _service.CreateEventAsync(TestAuthHandler.TestUserId, TestHelpers.MakeCreateDto(tagNames: []));

        result.Tags.Should().BeEmpty();
        (await Db.Tags.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task CreateEventAsync_TagNameExistsForOtherUser_CreatesNewTag()
    {
        var otherTag = new TagBuilder().WithUserId(OtherUserId).WithName("Work").Build();
        Db.Tags.Add(otherTag);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.CreateEventAsync(TestAuthHandler.TestUserId, TestHelpers.MakeCreateDto(tagNames: ["Work"]));

        result.Tags.Should().ContainSingle();
        result.Tags[0].Id.Should().NotBe(otherTag.Id);
        (await Db.Tags.CountAsync(t => t.Name == "Work", TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task CreateEventAsync_TagNamesWithWhitespace_AreTrimmed()
    {
        var result = await _service.CreateEventAsync(TestAuthHandler.TestUserId, TestHelpers.MakeCreateDto(tagNames: ["  Work  "]));

        result.Tags.Should().ContainSingle();
        result.Tags[0].Name.Should().Be("Work");
    }

    [Fact]
    public async Task CreateEventAsync_DuplicateTagNames_CreatesOnlyOneTag()
    {
        var userId = TestAuthHandler.TestUserId;
        var result = await _service.CreateEventAsync(userId, TestHelpers.MakeCreateDto(tagNames: ["Work", "Work"]));

        result.Tags.Should().ContainSingle();
        (await Db.Tags.CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task CreateEventAsync_IntensityZero_SavesSuccessfully()
    {
        var result = await _service.CreateEventAsync(TestAuthHandler.TestUserId, TestHelpers.MakeCreateDto(intensity: 0));

        result.Intensity.Should().Be(0);
    }

    [Fact]
    public async Task CreateEventAsync_IntensityTen_SavesSuccessfully()
    {
        var result = await _service.CreateEventAsync(TestAuthHandler.TestUserId, TestHelpers.MakeCreateDto(intensity: 10));

        result.Intensity.Should().Be(10);
    }

    #endregion

    #region UpdateEventAsync

    [Fact]
    public async Task UpdateEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "T", Intensity = 5 };
        var act = () => _service.UpdateEventAsync(Guid.Empty, Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateEventAsync_ValidDto_UpdatesAllFields()
    {
        var userId = TestAuthHandler.TestUserId;
        var newTs = new DateTimeOffset(2024, 8, 1, 18, 0, 0, TimeSpan.Zero);
        var ev = new EventBuilder()
            .WithUserId(userId)
            .WithTitle("Old Title")
            .WithType(EventType.Negative)
            .WithIntensity(3)
            .WithTimestamp(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero))
            .Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = new UpdateEventDto { Timestamp = newTs, Type = DtoEventType.Positive, Title = "New Title", Intensity = 9 };
        var result = await _service.UpdateEventAsync(userId, ev.Id, dto);

        var updated = await Db.Events.FirstAsync(TestContext.Current.CancellationToken);
        result.Should().BeTrue();
        updated.Type.Should().Be(EventType.Positive);
        updated.Title.Should().Be("New Title");
        updated.Intensity.Should().Be(9);
        updated.Timestamp.Should().Be(newTs);
    }

    [Fact]
    public async Task UpdateEventAsync_NonExistentEvent_ReturnsFalse()
    {
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Updated", Intensity = 5 };

        var result = await _service.UpdateEventAsync(TestAuthHandler.TestUserId, Guid.NewGuid(), dto);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateEventAsync_OtherUsersEvent_ReturnsFalse()
    {
        var ev = new EventBuilder().WithTitle("T").Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Hacked", Intensity = 5 };
        var result = await _service.UpdateEventAsync(Guid.NewGuid(), ev.Id, dto);

        result.Should().BeFalse();
        var unchanged = await Db.Events.FirstAsync(TestContext.Current.CancellationToken);
        unchanged.Title.Should().Be("T");
    }

    [Fact]
    public async Task UpdateEventAsync_WithNewTagName_AddsTag()
    {
        var userId = TestAuthHandler.TestUserId;
        var ev = new EventBuilder().WithUserId(userId).WithTitle("T").Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateEventAsync(userId, ev.Id, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Work"]
        });

        (await Db.EventTags.CountAsync(et => et.EventId == ev.Id, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateEventAsync_EmptyTagNames_RemovesAllTags()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new TagBuilder().WithUserId(userId).WithName("Work").Build();
        var ev = new EventBuilder().WithUserId(userId).WithTitle("T").Build();
        Db.Tags.Add(tag);
        Db.Events.Add(ev);
        Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateEventAsync(userId, ev.Id, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = []
        });

        (await Db.EventTags.CountAsync(et => et.EventId == ev.Id, TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task UpdateEventAsync_ReplacesExistingTagWithNewTag()
    {
        var userId = TestAuthHandler.TestUserId;
        var tagWork = new TagBuilder().WithUserId(userId).WithName("Work").Build();
        var ev = new EventBuilder().WithUserId(userId).WithTitle("T").Build();
        Db.Tags.Add(tagWork);
        Db.Events.Add(ev);
        Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tagWork.Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateEventAsync(userId, ev.Id, new UpdateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "T",
            Intensity = 5,
            TagNames = ["Health"]
        });

        var eventTags = await Db.EventTags.Where(et => et.EventId == ev.Id).ToListAsync(TestContext.Current.CancellationToken);
        eventTags.Should().ContainSingle();
        var tag = await Db.Tags.FindAsync([eventTags[0].TagId], TestContext.Current.CancellationToken);
        tag!.Name.Should().Be("Health");
    }

    #endregion

    #region DeleteEventAsync

    [Fact]
    public async Task DeleteEventAsync_EmptyUserId_ThrowsArgumentException()
    {
        var act = () => _service.DeleteEventAsync(Guid.Empty, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteEventAsync_ExistingEvent_RemovesIt()
    {
        var userId = TestAuthHandler.TestUserId;
        var ev = new EventBuilder().WithUserId(userId).WithTitle("Test").Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.DeleteEventAsync(userId, ev.Id);

        (await Db.Events.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteEventAsync_NonExistentEvent_DoesNotThrow()
    {
        var act = () => _service.DeleteEventAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteEventAsync_OtherUsersEvent_DoesNotDelete()
    {
        var ev = new EventBuilder().WithTitle("T").Build();
        Db.Events.Add(ev);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.DeleteEventAsync(Guid.NewGuid(), ev.Id);

        (await Db.Events.AnyAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEventAsync_EventWithTags_RemovesEventTags()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = new TagBuilder().WithUserId(userId).WithName("Work").Build();
        var ev = new EventBuilder().WithUserId(userId).WithTitle("T").Build();
        Db.Tags.Add(tag);
        Db.Events.Add(ev);
        Db.EventTags.Add(new EventTag { EventId = ev.Id, TagId = tag.Id });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.DeleteEventAsync(userId, ev.Id);

        (await Db.Events.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await Db.EventTags.AnyAsync(et => et.EventId == ev.Id, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    #endregion
}
