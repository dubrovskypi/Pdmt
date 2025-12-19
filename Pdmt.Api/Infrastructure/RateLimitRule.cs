namespace Pdmt.Api.Infrastructure
{
    public class RateLimitRule
    {
        public int MaxAttempts { get; set; }
        public int WindowSeconds { get; set; }
    }
}
