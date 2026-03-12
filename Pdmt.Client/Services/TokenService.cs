namespace Pdmt.Client.Services
{
    public class TokenService
    {
        private string? _accessToken;
        private string? _refreshToken;

        public string? AccessToken => _accessToken;
        public string? RefreshToken => _refreshToken;

        public event Action? OnChange;

        public void SetTokens(string accessToken, string refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            OnChange?.Invoke();
        }

        public void Clear()
        {
            _accessToken = null;
            _refreshToken = null;
            OnChange?.Invoke();
        }

        public bool IsAuthenticated => _accessToken != null;
    }
}
