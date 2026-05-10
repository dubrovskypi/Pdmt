using System.Net;
using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class AuthHeaderHandler(ITokenService tokenService) : DelegatingHandler
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await tokenService.IsAccessTokenExpiredAsync())
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                if (await tokenService.IsAccessTokenExpiredAsync())
                    await TryRefreshAsync(cancellationToken);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

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

        await _refreshLock.WaitAsync(cancellationToken);
        bool refreshed;
        try
        {
            // Another concurrent request may have already refreshed — check first
            var tokenAfterWait = await tokenService.GetAccessTokenAsync();
            if (tokenAfterWait is not null && tokenAfterWait != accessToken)
                refreshed = true;
            else
                refreshed = await TryRefreshAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }

        if (!refreshed)
        {
            await tokenService.ClearAsync();
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current.GoToAsync("//login"));
            }
            catch { }
            return response;
        }

        // Replay the original request with the new token
        var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (bodyBytes is not null)
        {
            retry.Content = new ByteArrayContent(bodyBytes);
            if (request.Content?.Headers is not null)
                foreach (var header in request.Content.Headers)
                    retry.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

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
            var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
            if (!refreshResponse.IsSuccessStatusCode)
                return false;

            var result = await refreshResponse.Content.ReadFromJsonAsync<AuthResultDto>(cancellationToken);
            if (result is null)
                return false;

            await tokenService.SetTokensAsync(result.AccessToken, result.AccessTokenExpiresAt, result.RefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
