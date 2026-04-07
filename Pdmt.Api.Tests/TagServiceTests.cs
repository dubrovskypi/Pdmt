using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Tests
{
    public class TagServiceTests
    {
        private AppDbContext CreateDbContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        #region GetTagsAsync

        [Fact]
        public async Task GetTagsAsync_ReturnsOnlyUserTags()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            db.Tags.AddRange(
                new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId1, CreatedAt = DateTimeOffset.UtcNow },
                new Tag { Id = Guid.NewGuid(), Name = "Personal", UserId = userId2, CreatedAt = DateTimeOffset.UtcNow }
            );
            await db.SaveChangesAsync();

            var result = await service.GetTagsAsync(userId1);

            Assert.Single(result);
            Assert.Equal("Work", result[0].Name);
        }

        [Fact]
        public async Task GetTagsAsync_OrdersByName()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            db.Tags.AddRange(
                new Tag { Id = Guid.NewGuid(), Name = "Zebra", UserId = userId, CreatedAt = DateTimeOffset.UtcNow },
                new Tag { Id = Guid.NewGuid(), Name = "Apple", UserId = userId, CreatedAt = DateTimeOffset.UtcNow },
                new Tag { Id = Guid.NewGuid(), Name = "Mango", UserId = userId, CreatedAt = DateTimeOffset.UtcNow }
            );
            await db.SaveChangesAsync();

            var result = await service.GetTagsAsync(userId);

            Assert.Equal("Apple", result[0].Name);
            Assert.Equal("Mango", result[1].Name);
            Assert.Equal("Zebra", result[2].Name);
        }

        [Fact]
        public async Task GetTagsAsync_ReturnsCorrectEventCount()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);

            var ev1 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "E1", Intensity = 5 };
            var ev2 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "E2", Intensity = 5 };
            var ev3 = new Event { Id = Guid.NewGuid(), UserId = userId, Timestamp = DateTimeOffset.UtcNow, Type = EventType.Positive, Title = "E3", Intensity = 5 };
            db.Events.AddRange(ev1, ev2, ev3);

            db.EventTags.AddRange(
                new EventTag { EventId = ev1.Id, TagId = tag.Id },
                new EventTag { EventId = ev2.Id, TagId = tag.Id },
                new EventTag { EventId = ev3.Id, TagId = tag.Id }
            );
            await db.SaveChangesAsync();

            var result = await service.GetTagsAsync(userId);

            Assert.Single(result);
            Assert.Equal(3, result[0].EventCount);
        }

        [Fact]
        public async Task GetTagsAsync_NoTags_ReturnsEmptyList()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var result = await service.GetTagsAsync(userId);

            Assert.Empty(result);
        }

        #endregion

        #region UpsertTagAsync

        [Fact]
        public async Task UpsertTagAsync_NewTag_CreatesAndReturns()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var dto = new CreateTagDto { Name = "Work" };
            var result = await service.UpsertTagAsync(userId, dto);

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Work", result.Name);
            Assert.Single(db.Tags);
        }

        [Fact]
        public async Task UpsertTagAsync_ExistingTag_ReturnsExisting()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var dto = new CreateTagDto { Name = "Work" };
            var result1 = await service.UpsertTagAsync(userId, dto);
            var result2 = await service.UpsertTagAsync(userId, dto);

            Assert.Equal(result1.Id, result2.Id);
            Assert.Single(db.Tags);
        }

        [Fact]
        public async Task UpsertTagAsync_TrimsWhitespace_MatchesExisting()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var dto1 = new CreateTagDto { Name = "Work" };
            var result1 = await service.UpsertTagAsync(userId, dto1);

            var dto2 = new CreateTagDto { Name = "  Work  " };
            var result2 = await service.UpsertTagAsync(userId, dto2);

            Assert.Equal(result1.Id, result2.Id);
            Assert.Single(db.Tags);
        }

        [Fact]
        public async Task UpsertTagAsync_SameNameDifferentUsers_CreatesTwo()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var dto = new CreateTagDto { Name = "Work" };
            await service.UpsertTagAsync(userId1, dto);
            await service.UpsertTagAsync(userId2, dto);

            Assert.Equal(2, db.Tags.Count());
        }

        #endregion

        #region DeleteTagAsync

        [Fact]
        public async Task DeleteTagAsync_ExistingTag_DeletesAndReturnsTrue()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            var result = await service.DeleteTagAsync(userId, tag.Id);

            Assert.True(result);
            Assert.Empty(db.Tags);
        }

        [Fact]
        public async Task DeleteTagAsync_NotFound_ReturnsFalse()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId = Guid.NewGuid();

            var result = await service.DeleteTagAsync(userId, Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteTagAsync_OtherUsersTag_ReturnsFalse()
        {
            var db = CreateDbContext();
            var service = new TagService(db);
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            var tag = new Tag { Id = Guid.NewGuid(), Name = "Work", UserId = userId1, CreatedAt = DateTimeOffset.UtcNow };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            var result = await service.DeleteTagAsync(userId2, tag.Id);

            Assert.False(result);
            Assert.Single(db.Tags);
        }

        #endregion
    }
}
