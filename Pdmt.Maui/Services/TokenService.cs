using System.Text.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class TokenService : ITokenService
{
    private const string TokensKey = "auth_tokens";

    // Legacy keys from the old storage format — removed on clear/migration
    private static readonly string[] LegacyKeys = ["access_token", "access_token_expires_at", "refresh_token"];

    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private StoredTokens? _cached;

    public async Task<string?> GetAccessTokenAsync() =>
        (await LoadAsync())?.AccessToken;

    public async Task<string?> GetRefreshTokenAsync() =>
        (await LoadAsync())?.RefreshToken;

    public async Task SetTokensAsync(
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        await _ioLock.WaitAsync();
        try
        {
            var tokens = new StoredTokens(accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt);
            await SecureStorage.Default.SetAsync(TokensKey, JsonSerializer.Serialize(tokens));
            _cached = tokens;
        }
        finally { _ioLock.Release(); }
    }

    public Task ClearAsync()
    {
        _cached = null;
        SecureStorage.Default.Remove(TokensKey);
        foreach (var key in LegacyKeys)
            SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync() =>
        await IsRefreshTokenValidAsync();

    public async Task<bool> IsAccessTokenExpiredAsync()
    {
        var tokens = await LoadAsync();
        if (tokens is null) return true;
        return DateTimeOffset.UtcNow >= tokens.AccessTokenExpiresAt;
    }

    public async Task<bool> IsRefreshTokenValidAsync()
    {
        var tokens = await LoadAsync();
        return tokens is not null && DateTimeOffset.UtcNow < tokens.RefreshTokenExpiresAt;
    }

    private async Task<StoredTokens?> LoadAsync()
    {
        if (_cached is not null) return _cached;

        await _ioLock.WaitAsync();
        try
        {
            if (_cached is not null) return _cached;
            var raw = await SecureStorage.Default.GetAsync(TokensKey);
            if (raw is null) return null;
            _cached = JsonSerializer.Deserialize<StoredTokens>(raw);
            return _cached;
        }
        catch { return null; }
        finally { _ioLock.Release(); }
    }
}
