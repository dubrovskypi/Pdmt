namespace Pdmt.Api.Services;

public interface IRateLimitService
{
    Task CheckAsync(string ruleName, string subject);
}
