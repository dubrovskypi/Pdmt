namespace Pdmt.Api.Dto
{
    public record WebAuthResultDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
}
