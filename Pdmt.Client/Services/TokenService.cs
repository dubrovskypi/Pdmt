namespace Pdmt.Client.Services
{
    public class TokenService
    {
        private string? _accessToken;
        private string? _refreshToken;

        public string? AccessToken => _accessToken;
        public string? RefreshToken => _refreshToken;

        public void SetTokens(string accessToken, string refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
        }

        public void Clear()
        {
            _accessToken = null;
            _refreshToken = null;
        }

        public bool IsAuthenticated => _accessToken != null;
    }
}
