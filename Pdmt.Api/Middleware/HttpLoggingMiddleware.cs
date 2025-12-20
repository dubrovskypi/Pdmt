using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Pdmt.Api.Middleware
{
    public class HttpLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HttpLoggingMiddleware> _logger;
        public HttpLoggingMiddleware(RequestDelegate next, ILogger<HttpLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            await _next(context);
            sw.Stop();
            var userid = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            _logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} ({Elapsed}ms) User:{UserId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                userid ?? "anonymous");
        }
    }
}
