using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : Controller
    {
        private readonly IEventService _eventService;

        public EventsController(IEventService eventService)
        {
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        }

        // GET /events
        // Supports filtering via query string:
        // ?from=&to=&type=&category=&isRelationship=&minIntensity=&maxIntensity=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? type = null,
            [FromQuery] string? category = null,
            [FromQuery] bool? isRelationship = null,
            [FromQuery] int? minIntensity = null,
            [FromQuery] int? maxIntensity = null)
        {
            var events = await _eventService.GetEventsAsync(from, to, type, category, isRelationship, minIntensity, maxIntensity);
            return Ok(events);
        }

        // GET /events/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EventDto>> GetEvent(Guid id)
        {
            var ev = await _eventService.GetByIdAsync(id);
            if (ev == null) return NotFound();
            return Ok(ev);
        }

        // POST /events
        [HttpPost]
        public async Task<ActionResult<EventDto>> CreateEvent([FromBody] EventDto model)
        {
            if (model == null) return BadRequest();
            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            await _eventService.CreateEventAsync(model);
            return CreatedAtAction(nameof(GetEvent), new { id = model.Id }, model);
        }

        // PUT /events/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] EventDto model)
        {
            if (model == null || id != model.Id) return BadRequest();

            var existing = await _eventService.GetByIdAsync(id);
            if (existing == null) return NotFound();

            await _eventService.UpdateEventAsync(model);
            return NoContent();
        }

        // DELETE /events/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteEvent(Guid id)
        {
            var existing = await _eventService.GetByIdAsync(id);
            if (existing == null) return NotFound();

            await _eventService.DeleteEventAsync(id);
            return NoContent();
        }

        // keep index for compatibility with existing views
        public IActionResult Index()
        {
            return View();
        }
    }
}
