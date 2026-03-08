using System.Net.Http.Headers;

namespace Pdmt.Client.Services
{
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly TokenService _tokenService;

        public AuthHeaderHandler(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_tokenService.AccessToken is not null)
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _tokenService.AccessToken);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
