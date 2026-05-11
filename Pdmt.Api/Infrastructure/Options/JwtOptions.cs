using System.ComponentModel.DataAnnotations;

namespace Pdmt.Api.Infrastructure.Options;

public class JwtOptions
{
    [Required, MinLength(1)] public string Issuer { get; set; } = null!;
    [Required, MinLength(1)] public string Audience { get; set; } = null!;
    [Range(1, 1440)] public int TokenLifetimeMinutes { get; set; }
    [Range(1, 365)] public int RefreshTokenLifetimeDays { get; set; }
}
