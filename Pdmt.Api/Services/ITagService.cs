using Pdmt.Api.Dto;

namespace Pdmt.Api.Services;

public interface ITagService
{
    Task<IReadOnlyList<TagResponseDto>> GetTagsAsync(Guid userId, CancellationToken ct);
    Task<TagResponseDto> UpsertTagAsync(Guid userId, CreateTagDto dto, CancellationToken ct);
    Task<bool> DeleteTagAsync(Guid userId, Guid tagId, CancellationToken ct);
}
