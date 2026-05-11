using System.Diagnostics.Metrics;

namespace Pdmt.Api.Infrastructure.Metrics;

public sealed class AuthMetrics
{
    public const string MeterName = "Pdmt.Auth";

    private readonly Counter<long> _loginAttempts;
    private readonly Counter<long> _refreshAttempts;
    private readonly Counter<long> _rateLimitTripped;

    public AuthMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _loginAttempts = meter.CreateCounter<long>("auth.login.attempts");
        _refreshAttempts = meter.CreateCounter<long>("auth.refresh.attempts");
        _rateLimitTripped = meter.CreateCounter<long>("rate_limit.tripped");
    }

    public void LoginAttempt(string outcome) =>
        _loginAttempts.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void RefreshAttempt(string outcome) =>
        _refreshAttempts.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void RateLimitTripped(string rule) =>
        _rateLimitTripped.Add(1, new KeyValuePair<string, object?>("rule", rule));
}
