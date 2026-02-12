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
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventsController(IEventService eventService)
        {
            _eventService = eventService;
        }

        // GET /events
        // Supports filtering via query string:
        // ?from=&to=&type=&category=&isRelationship=&minIntensity=&maxIntensity=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> GetEvents(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? type = null,
            [FromQuery] string? category = null,
            [FromQuery] bool? isRelationship = null,
            [FromQuery] int? minIntensity = null,
            [FromQuery] int? maxIntensity = null)
        {
            var userId = User.GetUserId();
            var events = await _eventService.GetEventsAsync(userId, from, to, type, category, isRelationship, minIntensity, maxIntensity);
            return Ok(events);
        }

        // GET /events/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EventResponseDto>> GetEvent(Guid id)
        {
            var userId = User.GetUserId();
            var ev = await _eventService.GetByIdAsync(userId, id);
            if (ev == null) return NotFound();
            return Ok(ev);
        }

        // POST /events
        [HttpPost]
        public async Task<ActionResult<EventResponseDto>> CreateEvent([FromBody] CreateEventDto model)
        {
            var userId = User.GetUserId();
            if (model == null) return BadRequest();
            var created = await _eventService.CreateEventAsync(userId, model);
            return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, created);
        }

        // PUT /events/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventDto model)
        {
            var userId = User.GetUserId();
            var existing = await _eventService.GetByIdAsync(userId, id);
            if (existing == null) return NotFound();
            await _eventService.UpdateEventAsync(userId, id, model);
            return NoContent();
        }

        // DELETE /events/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteEvent(Guid id)
        {
            var userId = User.GetUserId();
            var existing = await _eventService.GetByIdAsync(userId, id);
            if (existing == null) return NotFound();
            await _eventService.DeleteEventAsync(userId, id);
            return NoContent();
        }

        // GET /allevents
        [HttpGet("all")]
        [AllowAnonymous] //FOR DEBUG PURPOSES ONLY
        public async Task<ActionResult<EventResponseDto>> GetAllEvents()
        {
            var events = await _eventService.GetAllEventsAsync();
            return Ok(events);
        }
    }
}
