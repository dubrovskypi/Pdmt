using Microsoft.Extensions.Options;
using Pdmt.Client.Configuration;
using Pdmt.Client.Models;
using System.Net.Http.Json;

namespace Pdmt.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;

        public AuthService(IHttpClientFactory factory, IOptions<PdmtApiOptions> options)
        {
            _http = factory.CreateClient(options.Value.ClientName);
        }

        public async Task<AuthResultDto> LoginAsync(string email, string password)
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResultDto>())!;
        }

        public async Task<AuthResultDto> RegisterAsync(string email, string password)
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", new { email, password });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResultDto>())!;
        }

        public async Task LogoutAsync()
        {
            var response = await _http.PostAsync("api/auth/logout", null);
            response.EnsureSuccessStatusCode();
        }
    }
}
