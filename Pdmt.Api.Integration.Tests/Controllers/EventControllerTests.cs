using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace Pdmt.Api.Integration.Tests.Controllers;

public class EventsControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    #region GetEvents

    [Fact]
    public async Task GetEvents_Unauthenticated_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEvents_InvalidJwt_Returns401()
    {
        var response = await CreateJwtClient("invalid_token").GetAsync("/api/events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEvents_Authenticated_Returns200()
    {
        var response = await Client.GetAsync("/api/events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEvents_ValidJwt_Returns200()
    {
        var response = await CreateJwtClient(GenerateJwtToken()).GetAsync("/api/events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEvents_OtherUsersEventsExist_NotIncluded()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = OtherUserId, Email = "other@events-test.com", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Client.PostAsJsonAsync("/api/events", MakeDto("User A Secret Event", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);

        var otherClient = CreateJwtClient(GenerateJwtToken(OtherUserId));
        await otherClient.PostAsJsonAsync("/api/events", MakeDto("User B Event", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);

        var eventsA = await GetEventsItems(Client, "/api/events");
        var eventsB = await GetEventsItems(otherClient, "/api/events");

        eventsB.Should().NotContain(e => e.Title == "User A Secret Event");
        eventsB.Should().Contain(e => e.Title == "User B Event");
        eventsA.Should().NotContain(e => e.Title == "User B Event");
    }

    [Fact]
    public async Task GetEvents_FilterByType_ReturnsMatchingOnly()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Positive Event", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Negative Event", DtoEventType.Negative, 5), TestContext.Current.CancellationToken);

        var events = await GetEventsItems(jwtClient, "/api/events?type=Negative");

        events.Should().AllSatisfy(e => e.Type.Should().Be(DtoEventType.Negative));
        events.Should().NotContain(e => e.Title == "Positive Event");
    }

    [Fact]
    public async Task GetEvents_FilterByDateRange_ReturnsMatchingOnly()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("In Range", DtoEventType.Positive, 5, DateTimeOffset.UtcNow.AddDays(-3)), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Out Range", DtoEventType.Positive, 5, DateTimeOffset.UtcNow.AddDays(-10)), TestContext.Current.CancellationToken);

        var from = DateTimeOffset.UtcNow.AddDays(-5).ToString("O");
        var events = await GetEventsItems(jwtClient, $"/api/events?from={Uri.EscapeDataString(from)}");

        events.Should().Contain(e => e.Title == "In Range");
        events.Should().NotContain(e => e.Title == "Out Range");
    }

    [Fact]
    public async Task GetEvents_FilterByIntensityRange_ReturnsMatchingOnly()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Low", DtoEventType.Positive, 2), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Medium", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("High", DtoEventType.Positive, 9), TestContext.Current.CancellationToken);

        var events = await GetEventsItems(jwtClient, "/api/events?minIntensity=4&maxIntensity=6");

        events.Should().Contain(e => e.Title == "Medium");
        events.Should().NotContain(e => e.Title == "Low");
        events.Should().NotContain(e => e.Title == "High");
    }

    [Fact]
    public async Task GetEvents_FilterByTagIds_ReturnsMatchingOnly()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        var taggedDto = new CreateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Tagged", Intensity = 5, TagNames = ["FilterTag"] };
        var tagged = await (await jwtClient.PostAsJsonAsync("/api/events", taggedDto, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<EventResponseDto>(TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Untagged", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);

        var tagId = tagged!.Tags.Single(t => t.Name == "FilterTag").Id;
        var events = await GetEventsItems(jwtClient, $"/api/events?tags={tagId}");

        events.Should().Contain(e => e.Title == "Tagged");
        events.Should().NotContain(e => e.Title == "Untagged");
    }

    [Fact]
    public async Task GetEvents_InvalidTagIds_ReturnsAllEvents()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Event A", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Event B", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);

        var response = await jwtClient.GetAsync("/api/events?tags=not-a-guid,also-invalid", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EventResponseDto>>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEvents_Pagination_ReturnsCorrectPageAndTotal()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        for (var i = 0; i < 5; i++)
            await jwtClient.PostAsJsonAsync("/api/events", MakeDto($"Event {i}", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);

        var response = await jwtClient.GetAsync("/api/events?page=1&pageSize=3", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EventResponseDto>>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Total.Should().Be(5);
        result.Items.Should().HaveCount(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);

        var page2 = await (await jwtClient.GetAsync("/api/events?page=2&pageSize=3", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<PagedResult<EventResponseDto>>(TestContext.Current.CancellationToken);
        page2!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEvents_DefaultSort_TimestampDescending()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Oldest", DtoEventType.Positive, 5, DateTimeOffset.UtcNow.AddDays(-3)), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Newest", DtoEventType.Positive, 5, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Middle", DtoEventType.Positive, 5, DateTimeOffset.UtcNow.AddDays(-1)), TestContext.Current.CancellationToken);

        var items = await GetEventsItems(jwtClient, "/api/events");

        items[0].Title.Should().Be("Newest");
        items[1].Title.Should().Be("Middle");
        items[2].Title.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetEvents_PageSizeOver500_ClampedTo500()
    {
        var jwtClient = CreateJwtClient(GenerateJwtToken());
        await jwtClient.PostAsJsonAsync("/api/events", MakeDto("Event", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);

        var result = await (await jwtClient.GetAsync("/api/events?pageSize=9999", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<PagedResult<EventResponseDto>>(TestContext.Current.CancellationToken);

        result!.PageSize.Should().Be(500);
    }

    #endregion

    #region GetEvent

    [Fact]
    public async Task GetEvent_OwnEvent_Returns200()
    {
        var created = await CreateEventAndRead(Client, "My Event");

        var response = await Client.GetAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        var response = await Client.GetAsync($"/api/events/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEvent_OtherUsersEvent_Returns404()
    {
        var created = await CreateEventAndRead(Client, "Owner Only");

        var response = await CreateJwtClient(GenerateJwtToken(Guid.NewGuid())).GetAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region CreateEvent

    [Fact]
    public async Task CreateEvent_ValidRequest_Returns201WithLocation()
    {
        var response = await Client.PostAsJsonAsync("/api/events", MakeDto("Integration Test", DtoEventType.Positive, 5), TestContext.Current.CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<EventResponseDto>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(created!.Id.ToString());
    }

    [Fact]
    public async Task CreateEvent_ValidRequest_PersistsToDatabase()
    {
        var created = await CreateEventAndRead(Client, "Morning Run");

        var fetched = await (await Client.GetAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<EventResponseDto>(TestContext.Current.CancellationToken);

        fetched!.Title.Should().Be("Morning Run");
        fetched.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEvent_WithTags_ReturnsTagsInResponse()
    {
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "Tagged Event",
            Intensity = 5,
            TagNames = ["Work", "Health"]
        };

        var created = await (await Client.PostAsJsonAsync("/api/events", dto, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<EventResponseDto>(TestContext.Current.CancellationToken);

        created!.Tags.Should().HaveCount(2);
        created.Tags.Should().Contain(t => t.Name == "Work");
        created.Tags.Should().Contain(t => t.Name == "Health");
    }

    [Fact]
    public async Task CreateEvent_MissingTitle_Returns400()
    {
        var payload = new { Timestamp = DateTimeOffset.UtcNow, Type = 0, Intensity = 5 };

        var response = await Client.PostAsJsonAsync("/api/events", payload, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_IntensityOutOfRange_Returns400()
    {
        var payload = new { Timestamp = DateTimeOffset.UtcNow, Type = 0, Title = "Test", Intensity = 11 };

        var response = await Client.PostAsJsonAsync("/api/events", payload, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_IntensityZero_Returns201()
    {
        var payload = new { Timestamp = DateTimeOffset.UtcNow, Type = 0, Title = "Zero intensity", Intensity = 0 };

        var response = await Client.PostAsJsonAsync("/api/events", payload, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region UpdateEvent

    [Fact]
    public async Task UpdateEvent_OwnEvent_Returns204()
    {
        var created = await CreateEventAndRead(Client, "Original Title");
        var updateDto = new UpdateEventDto { Timestamp = created.Timestamp, Type = DtoEventType.Positive, Title = "Updated Title", Intensity = 8 };

        var updateResponse = await Client.PutAsJsonAsync($"/api/events/{created.Id}", updateDto, TestContext.Current.CancellationToken);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = await (await Client.GetAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<EventResponseDto>(TestContext.Current.CancellationToken);
        updated!.Title.Should().Be("Updated Title");
        updated.Intensity.Should().Be(8);
    }

    [Fact]
    public async Task UpdateEvent_NonExistentId_Returns404()
    {
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Ghost", Intensity = 5 };

        var response = await Client.PutAsJsonAsync($"/api/events/{Guid.NewGuid()}", dto, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEvent_OtherUsersEvent_Returns404()
    {
        var created = await CreateEventAndRead(Client, "Owner Only");
        var dto = new UpdateEventDto { Timestamp = created.Timestamp, Type = DtoEventType.Positive, Title = "Hacked", Intensity = 5 };

        var response = await CreateJwtClient(GenerateJwtToken(Guid.NewGuid())).PutAsJsonAsync($"/api/events/{created.Id}", dto, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DeleteEvent

    [Fact]
    public async Task DeleteEvent_OwnEvent_Returns204ThenGet404()
    {
        var created = await CreateEventAndRead(Client, "To Be Deleted");

        var deleteResponse = await Client.DeleteAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEvent_NonExistentId_Returns404()
    {
        var response = await Client.DeleteAsync($"/api/events/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEvent_OtherUsersEvent_Returns404()
    {
        var created = await CreateEventAndRead(Client, "Owner Only");

        var response = await CreateJwtClient(GenerateJwtToken(Guid.NewGuid())).DeleteAsync($"/api/events/{created.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    private HttpClient CreateJwtClient(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string GenerateJwtToken(Guid? userId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PostgresWebAppFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, (userId ?? TestUserHelper.TestUserId).ToString()) };
        var token = new JwtSecurityToken(
            issuer: PostgresWebAppFactory.TestJwtIssuer,
            audience: PostgresWebAppFactory.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static CreateEventDto MakeDto(string title, DtoEventType type, int intensity, DateTimeOffset? timestamp = null) =>
        new() { Title = title, Type = type, Intensity = intensity, Timestamp = timestamp ?? DateTimeOffset.UtcNow };

    private async Task<EventResponseDto> CreateEventAndRead(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/events", MakeDto(title, DtoEventType.Positive, 5), TestContext.Current.CancellationToken);
        return (await response.Content.ReadFromJsonAsync<EventResponseDto>(TestContext.Current.CancellationToken))!;
    }

    private async Task<IList<EventResponseDto>> GetEventsItems(HttpClient client, string url)
    {
        var result = await (await client.GetAsync(url, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<PagedResult<EventResponseDto>>(TestContext.Current.CancellationToken);
        return result!.Items.ToList();
    }
}
