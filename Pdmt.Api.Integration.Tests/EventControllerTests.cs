using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Dto;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace Pdmt.Api.Integration.Tests;

public class EventsControllerTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory = factory;

    #region GetEvents

    [Fact]
    public async Task GetEvents_AnonymousRequest_Returns401()
    {
        var client = CreateAnonymousClient();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_InvalidJwt_Returns401()
    {
        var client = CreateJwtClient("invalid_token");

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_Authenticated_Returns200()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_ValidJwt_Returns200()
    {
        var client = CreateJwtClient(GenerateJwtToken());

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_OtherUsersEvents_NotIncluded()
    {
        var clientA = CreateTestAuthClient();
        await clientA.PostAsJsonAsync("/api/events", MakeDto("User A Secret Event", DtoEventType.Positive, 5));

        var responseA = await clientA.GetAsync("/api/events");
        var eventsA = await responseA.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();
        Assert.Contains(eventsA!, e => e.Title == "User A Secret Event");

        var clientB = CreateJwtClient(GenerateJwtToken());
        var responseB = await clientB.GetAsync("/api/events");
        var eventsB = await responseB.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();

        Assert.DoesNotContain(eventsB!, e => e.Title == "User A Secret Event");
    }

    [Fact]
    public async Task GetEvents_FilterByType_ReturnsMatchingEvents()
    {
        var client = CreateJwtClient(GenerateJwtToken());
        await client.PostAsJsonAsync("/api/events", MakeDto("Positive Event", DtoEventType.Positive, 5));
        await client.PostAsJsonAsync("/api/events", MakeDto("Negative Event", DtoEventType.Negative, 5));

        var response = await client.GetAsync("/api/events?type=Negative");
        var events = await response.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();

        Assert.All(events!, e => Assert.Equal(DtoEventType.Negative, e.Type));
        Assert.DoesNotContain(events!, e => e.Title == "Positive Event");
    }

    [Fact]
    public async Task GetEvents_FilterByDateRange_ReturnsMatchingEvents()
    {
        var client = CreateJwtClient(GenerateJwtToken());
        await client.PostAsJsonAsync("/api/events", MakeDto("In Range",  DtoEventType.Positive, 5, DateTimeOffset.UtcNow.AddDays(-3)));
        await client.PostAsJsonAsync("/api/events", MakeDto("Out Range", DtoEventType.Positive, 5, DateTimeOffset.UtcNow.AddDays(-10)));

        var from = DateTimeOffset.UtcNow.AddDays(-5).ToString("O");
        var response = await client.GetAsync($"/api/events?from={Uri.EscapeDataString(from)}");
        var events = await response.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();

        Assert.Contains(events!, e => e.Title == "In Range");
        Assert.DoesNotContain(events!, e => e.Title == "Out Range");
    }

    [Fact]
    public async Task GetEvents_FilterByIntensityRange_ReturnsMatchingEvents()
    {
        var client = CreateJwtClient(GenerateJwtToken());
        await client.PostAsJsonAsync("/api/events", MakeDto("Low",    DtoEventType.Positive, 2));
        await client.PostAsJsonAsync("/api/events", MakeDto("Medium", DtoEventType.Positive, 5));
        await client.PostAsJsonAsync("/api/events", MakeDto("High",   DtoEventType.Positive, 9));

        var response = await client.GetAsync("/api/events?minIntensity=4&maxIntensity=6");
        var events = await response.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();

        Assert.Contains(events!, e => e.Title == "Medium");
        Assert.DoesNotContain(events!, e => e.Title == "Low");
        Assert.DoesNotContain(events!, e => e.Title == "High");
    }

    [Fact]
    public async Task GetEvents_FilterByTagIds_ReturnsTaggedEvents()
    {
        var client = CreateJwtClient(GenerateJwtToken());

        var taggedDto = new CreateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Tagged", Intensity = 5, TagNames = ["FilterTag"] };
        var tagged = await (await client.PostAsJsonAsync("/api/events", taggedDto)).Content.ReadFromJsonAsync<EventResponseDto>();
        await client.PostAsJsonAsync("/api/events", MakeDto("Untagged", DtoEventType.Positive, 5));

        var tagId = tagged!.Tags.Single(t => t.Name == "FilterTag").Id;
        var response = await client.GetAsync($"/api/events?tags={tagId}");
        var events = await response.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();

        Assert.Contains(events!, e => e.Title == "Tagged");
        Assert.DoesNotContain(events!, e => e.Title == "Untagged");
    }

    [Fact]
    public async Task GetEvents_InvalidTagIds_ReturnsAllEvents()
    {
        var client = CreateJwtClient(GenerateJwtToken());
        await client.PostAsJsonAsync("/api/events", MakeDto("Event A", DtoEventType.Positive, 5));
        await client.PostAsJsonAsync("/api/events", MakeDto("Event B", DtoEventType.Positive, 5));

        var response = await client.GetAsync("/api/events?tags=not-a-guid,also-invalid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();
        Assert.Equal(2, events!.Count());
    }

    #endregion

    #region GetEvent

    [Fact]
    public async Task GetEvent_NonExistentId_Returns404()
    {
        var client = CreateTestAuthClient();

        var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_OtherUsersEvent_Returns404()
    {
        var ownerClient = CreateTestAuthClient();
        var created = await CreateEventAndRead(ownerClient, "Owner Only");

        var otherClient = CreateJwtClient(GenerateJwtToken());
        var response = await otherClient.GetAsync($"/api/events/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region CreateEvent

    [Fact]
    public async Task CreateEvent_ValidDto_Returns201()
    {
        var client = CreateTestAuthClient();

        var response = await client.PostAsJsonAsync("/api/events", MakeDto("Integration Test", DtoEventType.Positive, 5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ValidDto_PersistedAndRetrievable()
    {
        var client = CreateTestAuthClient();

        var createResponse = await client.PostAsJsonAsync("/api/events", MakeDto("Morning Run", DtoEventType.Positive, 7));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<EventResponseDto>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/events/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<EventResponseDto>();
        Assert.Equal("Morning Run", fetched!.Title);
        Assert.Empty(fetched.Tags);
    }

    [Fact]
    public async Task CreateEvent_WithTags_ReturnsTagsInResponse()
    {
        var client = CreateTestAuthClient();
        var dto = new CreateEventDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = DtoEventType.Positive,
            Title = "Tagged Event",
            Intensity = 5,
            TagNames = ["Work", "Health"]
        };

        var response = await client.PostAsJsonAsync("/api/events", dto);
        var created = await response.Content.ReadFromJsonAsync<EventResponseDto>();

        Assert.Equal(2, created!.Tags.Count);
        Assert.Contains(created.Tags, t => t.Name == "Work");
        Assert.Contains(created.Tags, t => t.Name == "Health");
    }

    [Fact]
    public async Task CreateEvent_ValidDto_ReturnsLocationHeader()
    {
        var client = CreateTestAuthClient();

        var response = await client.PostAsJsonAsync("/api/events", MakeDto("Location Test", DtoEventType.Positive, 5));
        var created = await response.Content.ReadFromJsonAsync<EventResponseDto>();

        Assert.NotNull(response.Headers.Location);
        Assert.Contains(created!.Id.ToString(), response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CreateEvent_MissingTitle_Returns400()
    {
        var client = CreateTestAuthClient();
        var payload = new { Timestamp = DateTimeOffset.UtcNow, Type = 0, Intensity = 5 };

        var response = await client.PostAsJsonAsync("/api/events", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_IntensityOutOfRange_Returns400()
    {
        var client = CreateTestAuthClient();
        var payload = new { Timestamp = DateTimeOffset.UtcNow, Type = 0, Title = "Test", Intensity = 11 };

        var response = await client.PostAsJsonAsync("/api/events", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region UpdateEvent

    [Fact]
    public async Task UpdateEvent_ValidDto_Returns204AndUpdatesFields()
    {
        var client = CreateTestAuthClient();
        var created = await CreateEventAndRead(client, "Original Title");

        var updateDto = new UpdateEventDto { Timestamp = created.Timestamp, Type = DtoEventType.Positive, Title = "Updated Title", Intensity = 8 };
        var updateResponse = await client.PutAsJsonAsync($"/api/events/{created.Id}", updateDto);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await (await client.GetAsync($"/api/events/{created.Id}")).Content.ReadFromJsonAsync<EventResponseDto>();
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal(8, updated.Intensity);
    }

    [Fact]
    public async Task UpdateEvent_NonExistentId_Returns404()
    {
        var client = CreateTestAuthClient();
        var dto = new UpdateEventDto { Timestamp = DateTimeOffset.UtcNow, Type = DtoEventType.Positive, Title = "Ghost", Intensity = 5 };

        var response = await client.PutAsJsonAsync($"/api/events/{Guid.NewGuid()}", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_OtherUsersEvent_Returns404()
    {
        var ownerClient = CreateTestAuthClient();
        var created = await CreateEventAndRead(ownerClient, "Owner Only");

        var otherClient = CreateJwtClient(GenerateJwtToken());
        var dto = new UpdateEventDto { Timestamp = created.Timestamp, Type = DtoEventType.Positive, Title = "Hacked", Intensity = 5 };
        var response = await otherClient.PutAsJsonAsync($"/api/events/{created.Id}", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region DeleteEvent

    [Fact]
    public async Task DeleteEvent_ExistingEvent_Returns204AndEventGone()
    {
        var client = CreateTestAuthClient();
        var created = await CreateEventAndRead(client, "To Be Deleted");

        var deleteResponse = await client.DeleteAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_NonExistentId_Returns404()
    {
        var client = CreateTestAuthClient();

        var response = await client.DeleteAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_OtherUsersEvent_Returns404()
    {
        var ownerClient = CreateTestAuthClient();
        var created = await CreateEventAndRead(ownerClient, "Owner Only");

        var otherClient = CreateJwtClient(GenerateJwtToken());
        var response = await otherClient.DeleteAsync($"/api/events/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    private HttpClient CreateAnonymousClient() => _factory.CreateClient();

    private HttpClient CreateTestAuthClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("TestScheme");
        return client;
    }

    private HttpClient CreateJwtClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string GenerateJwtToken(Guid? userId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebAppFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString())
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static CreateEventDto MakeDto(string title, DtoEventType type, int intensity, DateTimeOffset? timestamp = null) =>
        new() { Title = title, Type = type, Intensity = intensity, Timestamp = timestamp ?? DateTimeOffset.UtcNow };

    private static async Task<EventResponseDto> CreateEventAndRead(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/events", MakeDto(title, DtoEventType.Positive, 5));
        return (await response.Content.ReadFromJsonAsync<EventResponseDto>())!;
    }
}
