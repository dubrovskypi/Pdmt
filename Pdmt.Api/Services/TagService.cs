using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;

namespace Pdmt.Api.Services;

public class TagService(AppDbContext db) : ITagService
{
    public async Task<IReadOnlyList<TagResponseDto>> GetTagsAsync(Guid userId)
    {
        return await db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new TagResponseDto
            {
                Id = t.Id,
                Name = t.Name,
                CreatedAt = t.CreatedAt
            })
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<TagResponseDto> UpsertTagAsync(Guid userId, CreateTagDto dto)
    {
        var normalizedName = dto.Name.Trim();

        var existing = await db.Tags
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Name == normalizedName);

        if (existing is not null)
            return new TagResponseDto
            {
                Id = existing.Id,
                Name = existing.Name,
                CreatedAt = existing.CreatedAt
            };

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        return new TagResponseDto
        {
            Id = tag.Id,
            Name = tag.Name,
            CreatedAt = tag.CreatedAt
        };
    }

    public async Task<bool> DeleteTagAsync(Guid userId, Guid tagId)
    {
        var tag = await db.Tags.Include(t => t.EventTags).FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
        if (tag is null) return false;
        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        return true;
    }
}
