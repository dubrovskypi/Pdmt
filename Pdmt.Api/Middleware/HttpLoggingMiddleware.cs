using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Pdmt.Api.Middleware;

public class HttpLoggingMiddleware(RequestDelegate next, ILogger<HttpLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<HttpLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();
        var userid = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub) 
            ?? "anonymous";
        Activity.Current?.SetTag("app.user.id", userid);
        _logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} ({Elapsed}ms) User:{UserId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            userid);
    }
}
