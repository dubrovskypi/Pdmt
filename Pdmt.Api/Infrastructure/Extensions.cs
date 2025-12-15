using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Pdmt.Api.Infrastructure
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var userid = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userid))
            {
                throw new InvalidOperationException("User identifier claim is missing.");
            }
            return Guid.Parse(userid);
        }
    }
}
