namespace Pdmt.Api.Services;

public record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
