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
    [ProducesResponseType(typeof(IEnumerable<TagResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetTags()
    {
        var userId = User.GetUserId();
        var tags = await tagService.GetTagsAsync(userId);
        return Ok(tags);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TagResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagResponseDto>> UpsertTag([FromBody] CreateTagDto dto)
    {
        var userId = User.GetUserId();
        var tag = await tagService.UpsertTagAsync(userId, dto);
        return Ok(tag);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        var userId = User.GetUserId();
        var deleted = await tagService.DeleteTagAsync(userId, id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
