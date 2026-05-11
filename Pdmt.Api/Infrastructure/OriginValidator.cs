namespace Pdmt.Api.Infrastructure;

public static class OriginValidator
{
    public static bool IsAllowed(string? origin, IReadOnlyList<string> allowedOrigins)
    {
        if (string.IsNullOrEmpty(origin)) return false;
        var normalized = origin.TrimEnd('/');
        return allowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase));
    }
}
