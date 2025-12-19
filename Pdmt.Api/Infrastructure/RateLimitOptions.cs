namespace Pdmt.Api.Infrastructure
{
    public class RateLimitOptions
    {
        public Dictionary<string, RateLimitRule> Rules { get; set; } = new();
    }
}
