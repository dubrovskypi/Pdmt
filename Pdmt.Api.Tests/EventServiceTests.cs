using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Services;

namespace Pdmt.Api.Tests
{
    public class EventServiceTests
    {
        private AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateEventAsync_Should_Create_Event_For_User()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var userId = Guid.NewGuid();
            var dto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = DtoEventType.Positive,
                Title = "Promotion",
                Intensity = 8,
                CanInfluence = true
            };

            var result = await service.CreateEventAsync(userId, dto);

            var entity = await db.Events.FirstOrDefaultAsync();
            Assert.NotNull(entity);
            Assert.Equal(userId, entity.UserId);
            Assert.Equal(dto.Title, entity.Title);
            Assert.Equal(entity.Id, result.Id);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_For_Other_User()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var entity = new Event
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                Timestamp = DateTime.UtcNow,
                Type = EventType.Positive,
                Title = "Test",
                Intensity = 5
            };
            db.Events.Add(entity);
            await db.SaveChangesAsync();

            var result = await service.GetByIdAsync(otherUserId, entity.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateEventAsync_Should_Return_False_If_Not_Found()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var dto = new UpdateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = DtoEventType.Positive,
                Title = "Updated",
                Intensity = 5
            };

            var result = await service.UpdateEventAsync(Guid.NewGuid(), Guid.NewGuid(), dto);

            Assert.False(result);
        }

        [Fact]
        public async Task CreateEventAsync_Should_Generate_Id()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var userId = Guid.NewGuid();
            var dto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = DtoEventType.Positive,
                Title = "Test",
                Intensity = 5
            };

            var result = await service.CreateEventAsync(userId, dto);

            Assert.NotEqual(Guid.Empty, result.Id);
        }

        [Fact]
        public async Task UpdateEventAsync_Should_Update_Event()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var userId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            db.Events.Add(new Event
            {
                Id = eventId,
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Type = EventType.Negative,
                Title = "Old Title",
                Intensity = 3
            });
            await db.SaveChangesAsync();
            var dto = new UpdateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = DtoEventType.Positive,
                Title = "New Title",
                Intensity = 9
            };

            var result = await service.UpdateEventAsync(userId, eventId, dto);

            var updated = await db.Events.FirstAsync();
            Assert.True(result);
            Assert.Equal(EventType.Positive, updated.Type);
            Assert.Equal("New Title", updated.Title);
            Assert.Equal(9, updated.Intensity);
        }

        [Fact]
        public async Task DeleteEventAsync_Should_Delete_Event()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var userId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            db.Events.Add(new Event
            {
                Id = eventId,
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Type = EventType.Positive,
                Title = "Test",
                Intensity = 5
            });
            await db.SaveChangesAsync();

            await service.DeleteEventAsync(userId, eventId);

            var exists = await db.Events.AnyAsync();
            Assert.False(exists);
        }

        [Fact]
        public async Task GetEventsAsync_Should_Filter_By_Tag()
        {
            var db = CreateDbContext();
            var service = new EventService(db);
            var userId = Guid.NewGuid();

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = "Work",
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            db.Tags.Add(tag);

            var eventWithTag = new Event
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Type = EventType.Positive,
                Title = "A",
                Intensity = 5
            };
            var eventWithoutTag = new Event
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Type = EventType.Positive,
                Title = "B",
                Intensity = 5
            };
            db.Events.AddRange(eventWithTag, eventWithoutTag);
            db.EventTags.Add(new EventTag { EventId = eventWithTag.Id, TagId = tag.Id });
            await db.SaveChangesAsync();

            var result = await service.GetEventsAsync(userId, null, null, null, [tag.Id], null, null);

            Assert.Single(result);
            Assert.Equal("A", result[0].Title);
        }
    }
}
