namespace Pdmt.Web.Models
{
    public class AuthResultDto
    {
        public string AccessToken { get; set; } = null!;
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; } = null!;
    }
}

//TODO Separate DTOs: Request DTOs and Response DTOs