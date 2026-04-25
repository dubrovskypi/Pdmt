using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using Pdmt.Api.Integration.Tests.Infrastructure.Builders;
using Pdmt.Api.Services;

namespace Pdmt.Api.Integration.Tests.Services;

public class TagServiceTests : ServiceTestBase
{
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private TagService _service = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Db.Users.Add(new UserBuilder().WithId(OtherUserId).WithEmail("other@pdmt.dev").Build());
        await Db.SaveChangesAsync();
        _service = new TagService(Db);
    }

    #region GetTagsAsync

    [Fact]
    public async Task GetTagsAsync_ReturnsOnlyUserTags()
    {
        Db.Tags.AddRange(
            TestHelpers.MakeTag(TestAuthHandler.TestUserId, "Work"),
            TestHelpers.MakeTag(OtherUserId, "Personal"));
        await Db.SaveChangesAsync();

        var result = await _service.GetTagsAsync(TestAuthHandler.TestUserId);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Work");
    }

    [Fact]
    public async Task GetTagsAsync_OrdersByName()
    {
        var userId = TestAuthHandler.TestUserId;
        Db.Tags.AddRange(
            TestHelpers.MakeTag(userId, "Zebra"),
            TestHelpers.MakeTag(userId, "Apple"),
            TestHelpers.MakeTag(userId, "Mango"));
        await Db.SaveChangesAsync();

        var result = await _service.GetTagsAsync(userId);

        result[0].Name.Should().Be("Apple");
        result[1].Name.Should().Be("Mango");
        result[2].Name.Should().Be("Zebra");
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsCorrectEventCount()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = TestHelpers.MakeTag(userId, "Work");
        Db.Tags.Add(tag);
        var ev1 = TestHelpers.MakeEvent(userId, "E1");
        var ev2 = TestHelpers.MakeEvent(userId, "E2");
        var ev3 = TestHelpers.MakeEvent(userId, "E3");
        Db.Events.AddRange(ev1, ev2, ev3);
        Db.EventTags.AddRange(
            new Domain.EventTag { EventId = ev1.Id, TagId = tag.Id },
            new Domain.EventTag { EventId = ev2.Id, TagId = tag.Id },
            new Domain.EventTag { EventId = ev3.Id, TagId = tag.Id });
        await Db.SaveChangesAsync();

        var result = await _service.GetTagsAsync(userId);

        result.Should().ContainSingle();
        result[0].EventCount.Should().Be(3);
    }

    [Fact]
    public async Task GetTagsAsync_NoTags_ReturnsEmptyList()
    {
        var result = await _service.GetTagsAsync(TestAuthHandler.TestUserId);

        result.Should().BeEmpty();
    }

    #endregion

    #region UpsertTagAsync

    [Fact]
    public async Task UpsertTagAsync_NewTag_CreatesAndReturns()
    {
        var dto = new CreateTagDto { Name = "Work" };

        var result = await _service.UpsertTagAsync(TestAuthHandler.TestUserId, dto);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Work");
        (await Db.Tags.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task UpsertTagAsync_ExistingTag_ReturnsExisting()
    {
        var userId = TestAuthHandler.TestUserId;
        var dto = new CreateTagDto { Name = "Work" };
        var result1 = await _service.UpsertTagAsync(userId, dto);

        var result2 = await _service.UpsertTagAsync(userId, dto);

        result2.Id.Should().Be(result1.Id);
        (await Db.Tags.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task UpsertTagAsync_TrimsWhitespace_MatchesExisting()
    {
        var userId = TestAuthHandler.TestUserId;
        var result1 = await _service.UpsertTagAsync(userId, new CreateTagDto { Name = "Work" });

        var result2 = await _service.UpsertTagAsync(userId, new CreateTagDto { Name = "  Work  " });

        result2.Id.Should().Be(result1.Id);
        (await Db.Tags.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task UpsertTagAsync_SameNameDifferentUsers_CreatesTwo()
    {
        var dto = new CreateTagDto { Name = "Work" };
        await _service.UpsertTagAsync(TestAuthHandler.TestUserId, dto);

        await _service.UpsertTagAsync(OtherUserId, dto);

        (await Db.Tags.CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    #endregion

    #region DeleteTagAsync

    [Fact]
    public async Task DeleteTagAsync_ExistingTag_DeletesAndReturnsTrue()
    {
        var userId = TestAuthHandler.TestUserId;
        var tag = TestHelpers.MakeTag(userId, "Work");
        Db.Tags.Add(tag);
        await Db.SaveChangesAsync();

        var result = await _service.DeleteTagAsync(userId, tag.Id);

        result.Should().BeTrue();
        (await Db.Tags.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTagAsync_NotFound_ReturnsFalse()
    {
        var result = await _service.DeleteTagAsync(TestAuthHandler.TestUserId, Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTagAsync_OtherUsersTag_ReturnsFalse()
    {
        var tag = TestHelpers.MakeTag(TestAuthHandler.TestUserId, "Work");
        Db.Tags.Add(tag);
        await Db.SaveChangesAsync();

        var result = await _service.DeleteTagAsync(OtherUserId, tag.Id);

        result.Should().BeFalse();
        (await Db.Tags.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    #endregion
}
