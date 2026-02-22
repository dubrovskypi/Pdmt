using Pdmt.Web.Models;

namespace Pdmt.Web.Services
{
    public class AuthService
    {
        private readonly IHttpClientFactory _httpFactory;

        public AuthService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        public async Task<AuthResultDto?> LoginAsync(string email, string password)
        {
            var client = _httpFactory.CreateClient("PdmtApi");
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { email, password });
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AuthResultDto>();
        }

        public async Task RegisterAsync(string email, string password)
        {
            var client = _httpFactory.CreateClient("PdmtApi");
            await client.PostAsJsonAsync("/api/auth/register",
                new { email, password });
        }
    }
}
