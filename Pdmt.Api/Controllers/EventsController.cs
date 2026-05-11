using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class EventsController(IEventService eventService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> GetEvents(
            CancellationToken ct,
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] DtoEventType? type = null,
            [FromQuery] string? tags = null,
            [FromQuery] int? minIntensity = null,
            [FromQuery] int? maxIntensity = null)
        {
            var userId = User.GetUserId();

            IReadOnlyList<Guid>? tagIds = null;
            if (!string.IsNullOrWhiteSpace(tags))
            {
                tagIds = tags
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();
            }

            var events = await eventService.GetEventsAsync(userId, from, to, type, tagIds, minIntensity, maxIntensity, ct);
            return Ok(events);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EventResponseDto>> GetEvent(Guid id, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var ev = await eventService.GetByIdAsync(userId, id, ct);
            if (ev == null) return NotFound();
            return Ok(ev);
        }

        [HttpPost]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<EventResponseDto>> CreateEvent([FromBody] CreateEventDto model, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var created = await eventService.CreateEventAsync(userId, model, ct);
            return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventDto model, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var existing = await eventService.GetByIdAsync(userId, id, ct);
            if (existing == null) return NotFound();
            await eventService.UpdateEventAsync(userId, id, model, ct);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var existing = await eventService.GetByIdAsync(userId, id, ct);
            if (existing == null) return NotFound();
            await eventService.DeleteEventAsync(userId, id, ct);
            return NoContent();
        }
    }
}
