using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Pdmt.Api.Integration.Tests.Controllers;

public class TagsControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    #region GetTags

    [Fact]
    public async Task GetTags_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/tags", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTags_Authenticated_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/tags", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<TagResponseDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTags_OtherUsersTagsExist_NotIncluded()
    {
        var otherUserId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = otherUserId, Email = $"{otherUserId}@test.com", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
            db.Tags.Add(new Tag { Id = Guid.NewGuid(), UserId = otherUserId, Name = "other-user-tag", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Client.GetAsync("/api/tags", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<TagResponseDto>>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain(t => t.Name == "other-user-tag");
    }

    #endregion

    #region UpsertTag

    [Fact]
    public async Task UpsertTag_NewName_Returns200WithTag()
    {
        var response = await Client.PostAsJsonAsync("/api/tags",
            new CreateTagDto { Name = "work" },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<TagResponseDto>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Id.Should().NotBeEmpty();
        body.Name.Should().Be("work");
    }

    [Fact]
    public async Task UpsertTag_DuplicateName_Returns200WithSameId()
    {
        var first = await Client.PostAsJsonAsync("/api/tags",
            new CreateTagDto { Name = "fitness" },
            TestContext.Current.CancellationToken);
        var firstBody = await first.Content.ReadFromJsonAsync<TagResponseDto>(
            TestContext.Current.CancellationToken);

        var second = await Client.PostAsJsonAsync("/api/tags",
            new CreateTagDto { Name = "fitness" },
            TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadFromJsonAsync<TagResponseDto>(
            TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody!.Id.Should().Be(firstBody!.Id);
    }

    #endregion

    #region DeleteTag

    [Fact]
    public async Task DeleteTag_OwnTag_Returns204()
    {
        var create = await Client.PostAsJsonAsync("/api/tags",
            new CreateTagDto { Name = "to-delete" },
            TestContext.Current.CancellationToken);
        var tag = await create.Content.ReadFromJsonAsync<TagResponseDto>(
            TestContext.Current.CancellationToken);

        var response = await Client.DeleteAsync($"/api/tags/{tag!.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTag_NotFound_Returns404()
    {
        var response = await Client.DeleteAsync($"/api/tags/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
