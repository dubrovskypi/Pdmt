namespace Pdmt.Client.Models
{
    public class AuthResultDto
    {
        public string AccessToken { get; set; } = null!;
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; } = null!;
    }
}
