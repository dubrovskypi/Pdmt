namespace Pdmt.Maui.Services;

public class TokenService : ITokenService
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public async Task<string?> GetAccessTokenAsync() =>
        await SecureStorage.Default.GetAsync(AccessTokenKey);

    public async Task<string?> GetRefreshTokenAsync() =>
        await SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        if (accessToken is not null) return true;
        var refreshToken = await GetRefreshTokenAsync();
        return refreshToken is not null;
    }
}
