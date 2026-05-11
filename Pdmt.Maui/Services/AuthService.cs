using System.Net;
using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class AuthService(IHttpClientFactory factory, ITokenService tokenService)
{
    public async Task<AuthResultDto> LoginAsync(string email, string password) =>
        await PostAuthAsync("api/auth/login", new { email, password });

    public async Task<AuthResultDto> RegisterAsync(string email, string password) =>
        await PostAuthAsync("api/auth/register", new { email, password });

    public async Task LogoutAsync()
    {
        var refreshToken = await tokenService.GetRefreshTokenAsync();
        var accessToken = await tokenService.GetAccessTokenAsync();
        try
        {
            if (refreshToken is not null)
            {
                var http = factory.CreateClient("PdmtAuth");
                using var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
                {
                    Content = JsonContent.Create(new { refreshToken })
                };
                if (accessToken is not null)
                    req.Headers.Authorization = new("Bearer", accessToken);
                await http.SendAsync(req);
            }
        }
        catch { /* network error — clear tokens locally anyway */ }
        finally { await tokenService.ClearAsync(); }
    }

    private async Task<AuthResultDto> PostAuthAsync(string endpoint, object body)
    {
        var http = factory.CreateClient("PdmtAuth");
        var resp = await http.PostAsJsonAsync(endpoint, body);
        if (!resp.IsSuccessStatusCode)
        {
            var message = "Request failed";
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
                if (!string.IsNullOrWhiteSpace(err?.Message)) message = err.Message;
            }
            catch { }
            throw new AuthException(message, resp.StatusCode);
        }
        return (await resp.Content.ReadFromJsonAsync<AuthResultDto>())!;
    }
}

// Mirrors Pdmt.Api/Infrastructure/ErrorResponse — not imported directly to avoid project reference
file sealed class ApiErrorResponse
{
    public string? Message { get; set; }
}
