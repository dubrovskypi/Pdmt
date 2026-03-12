using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Pdmt.Client.Configuration;
using Pdmt.Client.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pdmt.Client.Services
{
    public class AuthHeaderHandler(
        TokenService tokenService,
        NavigationManager nav,
        IOptions<PdmtApiOptions> options) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Buffer body so we can replay on retry (POST/PUT)
            byte[]? bodyBytes = null;
            string? contentType = null;
            if (request.Content is not null)
            {
                bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                contentType = request.Content.Headers.ContentType?.ToString();
            }

            if (tokenService.AccessToken is not null)
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokenService.AccessToken);

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                return response;

            if (tokenService.RefreshToken is null)
            {
                tokenService.Clear();
                nav.NavigateTo("/login");
                return response;
            }

            var refreshed = await TryRefreshAsync(cancellationToken);
            if (!refreshed)
            {
                tokenService.Clear();
                nav.NavigateTo("/login");
                return response;
            }

            // Retry original request with new access token
            var retry = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (bodyBytes is not null)
            {
                var content = new ByteArrayContent(bodyBytes);
                if (contentType is not null)
                    content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                retry.Content = content;
            }

            retry.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenService.AccessToken!);

            return await base.SendAsync(retry, cancellationToken);
        }

        private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
        {
            try
            {
                var baseUrl = options.Value.BaseUrl.TrimEnd('/');
                var refreshRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{baseUrl}/api/auth/refresh");
                refreshRequest.Content = JsonContent.Create(new { refreshToken = tokenService.RefreshToken });

                var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
                if (!refreshResponse.IsSuccessStatusCode) return false;

                var result = await refreshResponse.Content.ReadFromJsonAsync<AuthResultDto>(
                    cancellationToken: cancellationToken);
                if (result is null) return false;

                tokenService.SetTokens(result.AccessToken, result.RefreshToken);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
