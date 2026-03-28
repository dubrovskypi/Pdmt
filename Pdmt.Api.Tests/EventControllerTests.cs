using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace Pdmt.Api.Tests
{
    public class EventsControllerTests : IClassFixture<CustomWebAppFactory>
    {
        private readonly CustomWebAppFactory _factory;

        public EventsControllerTests(CustomWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetEvents_Should_Return_401_For_Anonymous()
        {
            var client = CreateAnonymousClient();

            var response = await client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_Should_Return_201()
        {
            var client = CreateTestAuthClient();
            var dto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = 1,
                Title = "Integration Test",
                Intensity = 5
            };

            var response = await client.PostAsJsonAsync("/api/events", dto);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task GetEvents_Should_Return_200_With_TestAuth()
        {
            var client = CreateTestAuthClient();

            var response = await client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Should_Return_401_With_Invalid_JWT()
        {
            var client = CreateJwtClient("invalid_token");

            var response = await client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Should_Return_200_With_Valid_JWT()
        {
            var client = CreateJwtClient(GenerateJwtToken());

            var response = await client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_Should_Persist_And_Be_Retrievable()
        {
            var client = CreateTestAuthClient();
            var dto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = 1,
                Title = "Morning Run",
                Intensity = 7
            };

            var createResponse = await client.PostAsJsonAsync("/api/events", dto);
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
        public async Task GetEvent_Should_Return_404_For_NonExistent_Id()
        {
            var client = CreateTestAuthClient();

            var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateEvent_Should_Return_204_And_Reflect_Changes()
        {
            var client = CreateTestAuthClient();

            var createDto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = 0,
                Title = "Original Title",
                Intensity = 3
            };
            var createResponse = await client.PostAsJsonAsync("/api/events", createDto);
            var created = await createResponse.Content.ReadFromJsonAsync<EventResponseDto>();

            var updateDto = new UpdateEventDto
            {
                Timestamp = created!.Timestamp,
                Type = 1,
                Title = "Updated Title",
                Intensity = 8
            };
            var updateResponse = await client.PutAsJsonAsync($"/api/events/{created.Id}", updateDto);
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/events/{created.Id}");
            var updated = await getResponse.Content.ReadFromJsonAsync<EventResponseDto>();
            Assert.Equal("Updated Title", updated!.Title);
            Assert.Equal(8, updated.Intensity);
        }

        [Fact]
        public async Task DeleteEvent_Should_Return_204_And_Event_Should_Be_Gone()
        {
            var client = CreateTestAuthClient();

            var createDto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = 1,
                Title = "To Be Deleted",
                Intensity = 4
            };
            var createResponse = await client.PostAsJsonAsync("/api/events", createDto);
            var created = await createResponse.Content.ReadFromJsonAsync<EventResponseDto>();

            var deleteResponse = await client.DeleteAsync($"/api/events/{created!.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/events/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task GetEvents_Should_Not_Return_Other_Users_Events()
        {
            var clientA = CreateTestAuthClient();
            var createDto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = 1,
                Title = "User A Secret Event",
                Intensity = 5
            };
            await clientA.PostAsJsonAsync("/api/events", createDto);

            var responseA = await clientA.GetAsync("/api/events");
            var eventsA = await responseA.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();
            Assert.Contains(eventsA!, e => e.Title == "User A Secret Event");

            var clientB = CreateJwtClient(GenerateJwtToken());
            var responseB = await clientB.GetAsync("/api/events");
            Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

            var eventsB = await responseB.Content.ReadFromJsonAsync<IEnumerable<EventResponseDto>>();
            Assert.DoesNotContain(eventsB!, e => e.Title == "User A Secret Event");
        }

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

        private string GenerateJwtToken()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-super-secret-test-key-123456"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            };
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
