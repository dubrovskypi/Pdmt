using System.Net;
using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class AuthHeaderHandler(ITokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.GetAccessTokenAsync();
        if (accessToken is not null)
            request.Headers.Authorization = new("Bearer", accessToken);

        // Buffer the request body so it can be replayed after a token refresh
        byte[]? bodyBytes = null;
        if (request.Content is not null)
            bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
            return response;

        var refreshed = await TryRefreshAsync(cancellationToken);
        if (!refreshed)
        {
            await tokenService.ClearAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync("//login"));
            return response;
        }

        // Replay the original request with the new token
        var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (bodyBytes is not null)
            retry.Content = new ByteArrayContent(bodyBytes);

        var newToken = await tokenService.GetAccessTokenAsync();
        retry.Headers.Authorization = new("Bearer", newToken!);

        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await tokenService.GetRefreshTokenAsync();
        if (refreshToken is null)
            return false;

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken })
        };

        try
        {
            var response = await base.SendAsync(refreshRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<AuthResultDto>(cancellationToken);
            if (result is null)
                return false;

            await tokenService.SetTokensAsync(result.AccessToken, result.RefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
