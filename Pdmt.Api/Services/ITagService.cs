using Pdmt.Api.Dto;

namespace Pdmt.Api.Services;

public interface ITagService
{
    Task<IReadOnlyList<TagResponseDto>> GetTagsAsync(Guid userId);
    Task<TagResponseDto> UpsertTagAsync(Guid userId, CreateTagDto dto);
    Task<bool> DeleteTagAsync(Guid userId, Guid tagId);
}
