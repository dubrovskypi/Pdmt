namespace Pdmt.Maui.Models;

public class AuthResultDto
{
    public required string AccessToken { get; set; }
    public DateTime AccessTokenExpiresAt { get; set; }
    public required string RefreshToken { get; set; }
}
