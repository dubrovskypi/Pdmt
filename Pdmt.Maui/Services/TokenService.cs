namespace Pdmt.Maui.Services;

public class TokenService : ITokenService
{
    private const string AccessTokenKey = "access_token";
    private const string AccessTokenExpiresAtKey = "access_token_expires_at";
    private const string RefreshTokenKey = "refresh_token";

    public async Task<string?> GetAccessTokenAsync() =>
        await SecureStorage.Default.GetAsync(AccessTokenKey);

    public async Task<string?> GetRefreshTokenAsync() =>
        await SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task SetTokensAsync(string accessToken, DateTimeOffset accessTokenExpiresAt, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(AccessTokenExpiresAtKey, accessTokenExpiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(AccessTokenExpiresAtKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync() =>
        await GetRefreshTokenAsync() is not null;

    public async Task<bool> IsAccessTokenExpiredAsync()
    {
        var raw = await SecureStorage.Default.GetAsync(AccessTokenExpiresAtKey);
        if (raw is null) return false;
        return DateTimeOffset.TryParse(raw, out var expiresAt) && DateTimeOffset.UtcNow >= expiresAt;
    }
}
