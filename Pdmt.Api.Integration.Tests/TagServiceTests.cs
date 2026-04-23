using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests
{
    public class TagServiceTests
    {
        private readonly AppDbContext _db;
        private readonly TagService _service;

        public TagServiceTests()
        {
            _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            _service = new TagService(_db);
        }

        #region GetTagsAsync

        [Fact]
        public async Task GetTagsAsync_ReturnsOnlyUserTags()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            _db.Tags.AddRange(
                TestHelpers.MakeTag(userId1, "Work"),
                TestHelpers.MakeTag(userId2, "Personal")
            );
            await _db.SaveChangesAsync();

            var result = await _service.GetTagsAsync(userId1);

            Assert.Single(result);
            Assert.Equal("Work", result[0].Name);
        }

        [Fact]
        public async Task GetTagsAsync_OrdersByName()
        {
            var userId = Guid.NewGuid();
            _db.Tags.AddRange(
                TestHelpers.MakeTag(userId, "Zebra"),
                TestHelpers.MakeTag(userId, "Apple"),
                TestHelpers.MakeTag(userId, "Mango")
            );
            await _db.SaveChangesAsync();

            var result = await _service.GetTagsAsync(userId);

            Assert.Equal("Apple", result[0].Name);
            Assert.Equal("Mango", result[1].Name);
            Assert.Equal("Zebra", result[2].Name);
        }

        [Fact]
        public async Task GetTagsAsync_ReturnsCorrectEventCount()
        {
            var userId = Guid.NewGuid();
            var tag = TestHelpers.MakeTag(userId, "Work");
            _db.Tags.Add(tag);
            var ev1 = TestHelpers.MakeEvent(userId, "E1");
            var ev2 = TestHelpers.MakeEvent(userId, "E2");
            var ev3 = TestHelpers.MakeEvent(userId, "E3");
            _db.Events.AddRange(ev1, ev2, ev3);
            _db.EventTags.AddRange(
                new EventTag { EventId = ev1.Id, TagId = tag.Id },
                new EventTag { EventId = ev2.Id, TagId = tag.Id },
                new EventTag { EventId = ev3.Id, TagId = tag.Id }
            );
            await _db.SaveChangesAsync();

            var result = await _service.GetTagsAsync(userId);

            Assert.Single(result);
            Assert.Equal(3, result[0].EventCount);
        }

        [Fact]
        public async Task GetTagsAsync_NoTags_ReturnsEmptyList()
        {
            var userId = Guid.NewGuid();

            var result = await _service.GetTagsAsync(userId);

            Assert.Empty(result);
        }

        #endregion

        #region UpsertTagAsync

        [Fact]
        public async Task UpsertTagAsync_NewTag_CreatesAndReturns()
        {
            var userId = Guid.NewGuid();
            var dto = new CreateTagDto { Name = "Work" };

            var result = await _service.UpsertTagAsync(userId, dto);

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Work", result.Name);
            Assert.Single(_db.Tags);
        }

        [Fact]
        public async Task UpsertTagAsync_ExistingTag_ReturnsExisting()
        {
            var userId = Guid.NewGuid();
            var dto = new CreateTagDto { Name = "Work" };
            var result1 = await _service.UpsertTagAsync(userId, dto);

            var result2 = await _service.UpsertTagAsync(userId, dto);

            Assert.Equal(result1.Id, result2.Id);
            Assert.Single(_db.Tags);
        }

        [Fact]
        public async Task UpsertTagAsync_TrimsWhitespace_MatchesExisting()
        {
            var userId = Guid.NewGuid();
            var dto1 = new CreateTagDto { Name = "Work" };
            var result1 = await _service.UpsertTagAsync(userId, dto1);
            var dto2 = new CreateTagDto { Name = "  Work  " };

            var result2 = await _service.UpsertTagAsync(userId, dto2);

            Assert.Equal(result1.Id, result2.Id);
            Assert.Single(_db.Tags);
        }

        [Fact]
        public async Task UpsertTagAsync_SameNameDifferentUsers_CreatesTwo()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            var dto = new CreateTagDto { Name = "Work" };
            await _service.UpsertTagAsync(userId1, dto);

            await _service.UpsertTagAsync(userId2, dto);

            Assert.Equal(2, _db.Tags.Count());
        }

        #endregion

        #region DeleteTagAsync

        [Fact]
        public async Task DeleteTagAsync_ExistingTag_DeletesAndReturnsTrue()
        {
            var userId = Guid.NewGuid();
            var tag = TestHelpers.MakeTag(userId, "Work");
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            var result = await _service.DeleteTagAsync(userId, tag.Id);

            Assert.True(result);
            Assert.Empty(_db.Tags);
        }

        [Fact]
        public async Task DeleteTagAsync_NotFound_ReturnsFalse()
        {
            var userId = Guid.NewGuid();

            var result = await _service.DeleteTagAsync(userId, Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteTagAsync_OtherUsersTag_ReturnsFalse()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            var tag = TestHelpers.MakeTag(userId1, "Work");
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            var result = await _service.DeleteTagAsync(userId2, tag.Id);

            Assert.False(result);
            Assert.Single(_db.Tags);
        }

        #endregion
    }
}
