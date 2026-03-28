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
    public class EventsController(IEventService eventService) : ControllerBase
    {
        // GET /events
        // Supports filtering via query string:
        // ?from=&to=&type=&tags=guid1,guid2&minIntensity=&maxIntensity=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> GetEvents(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? type = null,
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

            var events = await eventService.GetEventsAsync(userId, from, to, type, tagIds, minIntensity, maxIntensity);
            return Ok(events);
        }

        // GET /events/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EventResponseDto>> GetEvent(Guid id)
        {
            var userId = User.GetUserId();
            var ev = await eventService.GetByIdAsync(userId, id);
            if (ev == null) return NotFound();
            return Ok(ev);
        }

        // POST /events
        [HttpPost]
        public async Task<ActionResult<EventResponseDto>> CreateEvent([FromBody] CreateEventDto model)
        {
            var userId = User.GetUserId();
            if (model == null) return BadRequest();
            var created = await eventService.CreateEventAsync(userId, model);
            return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, created);
        }

        // PUT /events/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventDto model)
        {
            var userId = User.GetUserId();
            var existing = await eventService.GetByIdAsync(userId, id);
            if (existing == null) return NotFound();
            await eventService.UpdateEventAsync(userId, id, model);
            return NoContent();
        }

        // DELETE /events/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteEvent(Guid id)
        {
            var userId = User.GetUserId();
            var existing = await eventService.GetByIdAsync(userId, id);
            if (existing == null) return NotFound();
            await eventService.DeleteEventAsync(userId, id);
            return NoContent();
        }
    }
}
