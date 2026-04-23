using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Controllers;

public class EventsControllerTests
{
    private readonly Mock<IEventService> _eventService = new();
    private readonly EventsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public EventsControllerTests()
    {
        _sut = new EventsController(_eventService.Object)
        {
            ControllerContext = BuildContext(_userId)
        };
    }

    private static ControllerContext BuildContext(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())]))
        }
    };

    [Fact]
    public async Task GetEvents_NoFilter_Returns200AndCallsService()
    {
        IReadOnlyList<EventResponseDto> events = [new() { Id = Guid.NewGuid(), Title = "T", Type = DtoEventType.Positive, Intensity = 5 }];
        _eventService
            .Setup(s => s.GetEventsAsync(_userId, null, null, null, null, null, null))
            .ReturnsAsync(events);

        var result = await _sut.GetEvents();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(events);
    }

    [Fact]
    public async Task GetEvents_WithTagsQueryString_ParsesValidGuids()
    {
        var tagId = Guid.NewGuid();
        _eventService
            .Setup(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<DtoEventType?>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([]);

        await _sut.GetEvents(tags: tagId.ToString());

        _eventService.Verify(s => s.GetEventsAsync(
            _userId, null, null, null,
            It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Count == 1 && ids[0] == tagId),
            null, null), Times.Once);
    }

    [Fact]
    public async Task GetEvents_WithMixedInvalidTagGuids_FiltersOutInvalid()
    {
        var tagId = Guid.NewGuid();
        _eventService
            .Setup(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<DtoEventType?>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([]);

        await _sut.GetEvents(tags: $"{tagId},not-a-guid,also-invalid");

        _eventService.Verify(s => s.GetEventsAsync(
            _userId, null, null, null,
            It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Count == 1 && ids[0] == tagId),
            null, null), Times.Once);
    }

    [Fact]
    public async Task GetEvent_EventFound_Returns200()
    {
        var id = Guid.NewGuid();
        var ev = new EventResponseDto { Id = id, Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        _eventService.Setup(s => s.GetByIdAsync(_userId, id)).ReturnsAsync(ev);

        var result = await _sut.GetEvent(id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(ev);
    }

    [Fact]
    public async Task GetEvent_EventNotFound_Returns404()
    {
        _eventService
            .Setup(s => s.GetByIdAsync(_userId, It.IsAny<Guid>()))
            .ReturnsAsync((EventResponseDto?)null);

        var result = await _sut.GetEvent(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateEvent_ValidDto_Returns201WithCreatedAtLocation()
    {
        var dto = new CreateEventDto { Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        var created = new EventResponseDto { Id = Guid.NewGuid(), Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        _eventService.Setup(s => s.CreateEventAsync(_userId, dto)).ReturnsAsync(created);

        var result = await _sut.CreateEvent(dto);

        var createdAt = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdAt.StatusCode.Should().Be(201);
        createdAt.ActionName.Should().Be(nameof(_sut.GetEvent));
        createdAt.Value.Should().Be(created);
    }

    [Fact]
    public async Task UpdateEvent_EventNotFound_Returns404_DoesNotCallUpdate()
    {
        var id = Guid.NewGuid();
        _eventService.Setup(s => s.GetByIdAsync(_userId, id)).ReturnsAsync((EventResponseDto?)null);

        var result = await _sut.UpdateEvent(id, new UpdateEventDto { Title = "T" });

        result.Should().BeOfType<NotFoundResult>();
        _eventService.Verify(
            s => s.UpdateEventAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateEventDto>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateEvent_EventFound_Returns204()
    {
        var id = Guid.NewGuid();
        var existing = new EventResponseDto { Id = id, Title = "T", Type = DtoEventType.Positive, Intensity = 5 };
        _eventService.Setup(s => s.GetByIdAsync(_userId, id)).ReturnsAsync(existing);
        _eventService.Setup(s => s.UpdateEventAsync(_userId, id, It.IsAny<UpdateEventDto>())).ReturnsAsync(true);

        var result = await _sut.UpdateEvent(id, new UpdateEventDto { Title = "T" });

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteEvent_EventNotFound_Returns404_DoesNotCallDelete()
    {
        var id = Guid.NewGuid();
        _eventService.Setup(s => s.GetByIdAsync(_userId, id)).ReturnsAsync((EventResponseDto?)null);

        var result = await _sut.DeleteEvent(id);

        result.Should().BeOfType<NotFoundResult>();
        _eventService.Verify(
            s => s.DeleteEventAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteEvent_EventFound_Returns204()
    {
        var id = Guid.NewGuid();
        var existing = new EventResponseDto { Id = id, Title = "T", Type = DtoEventType.Positive, Intensity = 5 };
        _eventService.Setup(s => s.GetByIdAsync(_userId, id)).ReturnsAsync(existing);
        _eventService.Setup(s => s.DeleteEventAsync(_userId, id)).Returns(Task.CompletedTask);

        var result = await _sut.DeleteEvent(id);

        result.Should().BeOfType<NoContentResult>();
    }
}
