using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Extensions;
using Pdmt.Api.Services;

namespace Pdmt.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class TagsController(ITagService tagService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TagResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TagResponseDto>>> GetTags(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var tags = await tagService.GetTagsAsync(userId, ct);
        return Ok(tags);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TagResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagResponseDto>> UpsertTag([FromBody] CreateTagDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var tag = await tagService.UpsertTagAsync(userId, dto, ct);
        return Ok(tag);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var deleted = await tagService.DeleteTagAsync(userId, id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
