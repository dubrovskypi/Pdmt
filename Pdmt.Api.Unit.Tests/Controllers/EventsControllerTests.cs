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

    #region GetEvents

    [Fact]
    public async Task GetEvents_NoFilter_Returns200AndCallsService()
    {
        var pagedResult = new PagedResult<EventResponseDto>(
            [new() { Id = Guid.NewGuid(), Title = "T", Type = DtoEventType.Positive, Intensity = 5 }],
            Total: 1, Page: 1, PageSize: 100);
        _eventService
            .Setup(s => s.GetEventsAsync(_userId, null, null, null, null, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetEvents(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(pagedResult);
    }

    [Fact]
    public async Task GetEvents_WithTagsQueryString_ParsesValidGuids()
    {
        var tagId = Guid.NewGuid();
        var emptyResult = new PagedResult<EventResponseDto>([], 0, 1, 100);
        _eventService
            .Setup(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<DtoEventType?>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        await _sut.GetEvents(CancellationToken.None, tags: tagId.ToString());

        _eventService.Verify(s => s.GetEventsAsync(
            _userId, null, null, null,
            It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Count == 1 && ids[0] == tagId),
            null, null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEvents_WithMixedInvalidTagGuids_FiltersOutInvalid()
    {
        var tagId = Guid.NewGuid();
        var emptyResult = new PagedResult<EventResponseDto>([], 0, 1, 100);
        _eventService
            .Setup(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<DtoEventType?>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        await _sut.GetEvents(CancellationToken.None, tags: $"{tagId},not-a-guid,also-invalid");

        _eventService.Verify(s => s.GetEventsAsync(
            _userId, null, null, null,
            It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Count == 1 && ids[0] == tagId),
            null, null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetEvent

    [Fact]
    public async Task GetEvent_EventFound_Returns200()
    {
        var id = Guid.NewGuid();
        var ev = new EventResponseDto { Id = id, Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        _eventService.Setup(s => s.GetByIdAsync(_userId, id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);

        var result = await _sut.GetEvent(id, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(ev);
    }

    [Fact]
    public async Task GetEvent_EventNotFound_Returns404()
    {
        _eventService
            .Setup(s => s.GetByIdAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResponseDto?)null);

        var result = await _sut.GetEvent(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region CreateEvent

    [Fact]
    public async Task CreateEvent_ValidDto_Returns201WithCreatedAtLocation()
    {
        var dto = new CreateEventDto { Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        var created = new EventResponseDto { Id = Guid.NewGuid(), Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        _eventService.Setup(s => s.CreateEventAsync(_userId, dto, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await _sut.CreateEvent(dto, CancellationToken.None);

        var createdAt = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdAt.StatusCode.Should().Be(201);
        createdAt.ActionName.Should().Be(nameof(_sut.GetEvent));
        createdAt.Value.Should().Be(created);
    }

    #endregion

    #region UpdateEvent

    [Fact]
    public async Task UpdateEvent_ServiceReturnsFalse_Returns404()
    {
        var id = Guid.NewGuid();
        _eventService.Setup(s => s.UpdateEventAsync(_userId, id, It.IsAny<UpdateEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.UpdateEvent(id, new UpdateEventDto { Title = "T" }, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateEvent_ServiceReturnsTrue_Returns204()
    {
        var id = Guid.NewGuid();
        _eventService.Setup(s => s.UpdateEventAsync(_userId, id, It.IsAny<UpdateEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdateEvent(id, new UpdateEventDto { Title = "T" }, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    #endregion

    #region DeleteEvent

    [Fact]
    public async Task DeleteEvent_ServiceReturnsFalse_Returns404()
    {
        var id = Guid.NewGuid();
        _eventService.Setup(s => s.DeleteEventAsync(_userId, id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.DeleteEvent(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteEvent_ServiceReturnsTrue_Returns204()
    {
        var id = Guid.NewGuid();
        _eventService.Setup(s => s.DeleteEventAsync(_userId, id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.DeleteEvent(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    #endregion
}
