using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class AuthService(IHttpClientFactory factory, ITokenService tokenService)
{
    public async Task<AuthResultDto> LoginAsync(string email, string password)
    {
        var http = factory.CreateClient("PdmtApi");
        var response = await http.PostAsJsonAsync("api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!;
    }

    public async Task LogoutAsync()
    {
        var http = factory.CreateClient("PdmtApi");
        await http.PostAsync("api/auth/logout", null);
        await tokenService.ClearAsync();
    }
}
