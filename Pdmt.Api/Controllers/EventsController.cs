using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;
using System.Security.Claims;

namespace Pdmt.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : Controller
    {
        private readonly IEventService _eventService;
        private readonly IUserService _userService;

        public EventsController(IEventService eventService, IUserService userService)
        {
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        // GET /events
        // Supports filtering via query string:
        // ?from=&to=&type=&category=&isRelationship=&minIntensity=&maxIntensity=
        [HttpGet]
        [AllowAnonymous] //TODO restrict to authorized users only
        public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? type = null,
            [FromQuery] string? category = null,
            [FromQuery] bool? isRelationship = null,
            [FromQuery] int? minIntensity = null,
            [FromQuery] int? maxIntensity = null)
        {
            //var userId = _userService.GetUserId();
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var events = await _eventService.GetEventsAsync(userId, from, to, type, category, isRelationship, minIntensity, maxIntensity);
            return Ok(events);
        }

        // GET /events/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EventDto>> GetEvent(Guid id)
        {
            //var userId = _userService.GetUserId();
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var ev = await _eventService.GetByIdAsync(userId, id);
            if (ev == null) return NotFound();
            return Ok(ev);
        }

        // POST /events
        [HttpPost]
        public async Task<ActionResult<EventDto>> CreateEvent([FromBody] EventDto model)
        {
            var userId = _userService.GetUserId();
            if (model == null) return BadRequest();
            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            await _eventService.CreateEventAsync(userId, model);
            return CreatedAtAction(nameof(GetEvent), new { id = model.Id }, model);
        }

        // PUT /events/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] EventDto model)
        {
            var userId = _userService.GetUserId();
            if (model == null || id != model.Id) return BadRequest();

            var existing = await _eventService.GetByIdAsync(userId, id);
            if (existing == null) return NotFound();

            await _eventService.UpdateEventAsync(userId, id, model);
            return NoContent();
        }

        // DELETE /events/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteEvent(Guid id)
        {
            var userId = _userService.GetUserId();
            var existing = await _eventService.GetByIdAsync(userId, id);
            if (existing == null) return NotFound();

            await _eventService.DeleteEventAsync(userId, id);
            return NoContent();
        }

        // keep index for compatibility with existing views
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }
    }
}
