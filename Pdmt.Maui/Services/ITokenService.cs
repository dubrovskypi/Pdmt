namespace Pdmt.Maui.Services;

public interface ITokenService
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokensAsync(string accessToken, DateTimeOffset accessTokenExpiresAt, string refreshToken);
    Task ClearAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<bool> IsAccessTokenExpiredAsync();
}
