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
        private readonly HttpClient _client;

        public EventsControllerTests(CustomWebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetEvents_Should_Return_401_Without_Token()
        {
            var response = await _client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_Should_Return_201()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");
            var dto = new CreateEventDto
            {
                Timestamp = DateTime.UtcNow,
                Type = 1,
                Category = "Work",
                Title = "Integration Test",
                Intensity = 5
            };

            var response = await _client.PostAsJsonAsync("/api/events", dto);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task GetEvents_Should_Return_401_Without_Header()
        {
            var response = await _client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetEvents_Should_Return_200_With_TestAuth()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");

            var response = await _client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Should_Return_401_With_Invalid_JWT()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid_token");

            var response = await _client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Should_Return_200_With_Valid_JWT()
        {
            var token = GenerateJwtToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
