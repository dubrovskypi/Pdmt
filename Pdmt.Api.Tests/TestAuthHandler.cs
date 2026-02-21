using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Pdmt.Api.Tests
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestScheme";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Task.FromResult(AuthenticateResult.Fail("No header"));

            var authHeader = Request.Headers["Authorization"].ToString();
            // Only handle requests where Authorization header starts with the TestScheme.
            // Otherwise return NoResult so another handler (e.g., JwtBearer) can handle it.
            if (!authHeader.StartsWith(SchemeName, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
