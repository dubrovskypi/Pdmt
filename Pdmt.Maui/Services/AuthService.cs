using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class AuthService(IHttpClientFactory factory, ITokenService tokenService)
{
    private readonly HttpClient _http = factory.CreateClient("PdmtApi");

    public async Task<AuthResultDto> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!;
    }

    public async Task LogoutAsync()
    {
        await _http.PostAsync("api/auth/logout", null);
        await tokenService.ClearAsync();
    }
}
