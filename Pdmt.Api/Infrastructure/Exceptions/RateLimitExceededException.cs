namespace Pdmt.Api.Infrastructure.Exceptions
{
    public class RateLimitExceededException : Exception
    {
        public string Rule { get; }

        public RateLimitExceededException(string rule) : base($"Rate limit exceeded: {rule}")
        {
            Rule = rule;
        }
    }
}
