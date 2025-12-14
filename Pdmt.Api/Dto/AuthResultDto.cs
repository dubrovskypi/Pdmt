namespace Pdmt.Api.Dto
{
    public class AuthResultDto
    {
        public string Token { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
