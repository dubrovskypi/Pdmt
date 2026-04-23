using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pdmt.Api.Controllers;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Controllers;

public class TagsControllerTests
{
    private readonly Mock<ITagService> _tagService = new();
    private readonly TagsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TagsControllerTests()
    {
        _sut = new TagsController(_tagService.Object)
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
    public async Task GetTags_Returns200WithList()
    {
        IReadOnlyList<TagResponseDto> tags =
        [
            new() { Id = Guid.NewGuid(), Name = "stress" },
            new() { Id = Guid.NewGuid(), Name = "joy" }
        ];
        _tagService.Setup(s => s.GetTagsAsync(_userId)).ReturnsAsync(tags);

        var result = await _sut.GetTags();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(tags);
    }

    [Fact]
    public async Task UpsertTag_Returns200WithTag()
    {
        var dto = new CreateTagDto { Name = "stress" };
        var tag = new TagResponseDto { Id = Guid.NewGuid(), Name = "stress" };
        _tagService.Setup(s => s.UpsertTagAsync(_userId, dto)).ReturnsAsync(tag);

        var result = await _sut.UpsertTag(dto);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(tag);
    }

    [Fact]
    public async Task DeleteTag_TagFound_Returns204()
    {
        var tagId = Guid.NewGuid();
        _tagService.Setup(s => s.DeleteTagAsync(_userId, tagId)).ReturnsAsync(true);

        var result = await _sut.DeleteTag(tagId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteTag_TagNotFound_Returns404()
    {
        var tagId = Guid.NewGuid();
        _tagService.Setup(s => s.DeleteTagAsync(_userId, tagId)).ReturnsAsync(false);

        var result = await _sut.DeleteTag(tagId);

        result.Should().BeOfType<NotFoundResult>();
    }
}
