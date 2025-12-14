using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Pdmt.Api.Infrastructure
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            return Guid.Parse(
                user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        }
    }
}
