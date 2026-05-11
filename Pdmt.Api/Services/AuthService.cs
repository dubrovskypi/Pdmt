using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Infrastructure.Metrics;
using Pdmt.Api.Infrastructure.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Services;

public class AuthService(
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    IRateLimitService rateLimit,
    SigningCredentials signingCreds,
    AuthMetrics metrics,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly TimeSpan RefreshTokenGracePeriod = TimeSpan.FromSeconds(30);
    private readonly SigningCredentials _signingCredentials = signingCreds;
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResult> RegisterAsync(UserDto dto, string ip, CancellationToken ct)
    {
        await rateLimit.CheckAsync("Auth.Register", ip);

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (exists)
            throw new ValidationException("User already exists");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var (refreshTokenEntity, rawRefreshToken) = CreateRefreshToken(user, Guid.NewGuid());
        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync(ct);

        var accessToken = GenerateAccessToken(user);
        return new AuthResult(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshTokenEntity.ExpiresAt);
    }

    private const int LockoutFailureThreshold = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    public async Task<AuthResult> LoginAsync(UserDto dto, string ip, CancellationToken ct)
    {
        await rateLimit.CheckAsync("Auth.Login", ip);

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var cutoff = DateTimeOffset.UtcNow.Subtract(LockoutWindow);
        var recentFails = await db.FailedLoginAttempts
            .CountAsync(f => f.Email == normalizedEmail && f.OccurredAtUtc >= cutoff, ct);
        if (recentFails >= LockoutFailureThreshold)
        {
            logger.LogWarning("auth.login.locked email:{Email} ip:{Ip} recentFails:{RecentFails}", normalizedEmail, ip, recentFails);
            metrics.LoginAttempt("locked");
            throw new UnauthorizedAccessException("Account temporarily locked. Try again later.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            logger.LogWarning("auth.login.fail email:{Email} ip:{Ip}", normalizedEmail, ip);
            metrics.LoginAttempt("invalid_credentials");
            db.FailedLoginAttempts.Add(new FailedLoginAttempt
            {
                Email = normalizedEmail,
                IpAddress = ip,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Reason = "Invalid credentials"
            });
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var (refreshTokenEntity, rawRefreshToken) = CreateRefreshToken(user, Guid.NewGuid());
        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("auth.login.success userId:{UserId} ip:{Ip}", user.Id, ip);
        metrics.LoginAttempt("success");
        var accessToken = GenerateAccessToken(user);
        return new AuthResult(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshTokenEntity.ExpiresAt);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string ip, CancellationToken ct)
    {
        await rateLimit.CheckAsync("Auth.Refresh", ip);

        var hashedRefreshToken = HashToken(refreshToken);
        var token = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == hashedRefreshToken && rt.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (token is null)
        {
            logger.LogWarning("auth.refresh.invalid ip:{Ip}", ip);
            metrics.RefreshAttempt("invalid_token");
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (!token.IsRevoked)
        {
            // Atomic revoke: ensures only one concurrent request succeeds
            var revoked = await db.RefreshTokens
                .Where(rt => rt.Id == token.Id && !rt.IsRevoked)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RotatedAt, DateTimeOffset.UtcNow), ct);

            if (revoked > 0)
            {
                var (newRt, rawRt) = CreateRefreshToken(token.User, token.FamilyId);
                db.RefreshTokens.Add(newRt);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("auth.refresh.success userId:{UserId}", token.UserId);
                metrics.RefreshAttempt("success");
                var newAccess = GenerateAccessToken(token.User);
                return new AuthResult(newAccess.Token, newAccess.ExpiresAt, rawRt, newRt.ExpiresAt);
            }

            // Race: another parallel request just revoked this token — reload to get RotatedAt
            await db.Entry(token).ReloadAsync(ct);
        }

        // Token is revoked — check grace window for idempotent response
        if (token.RotatedAt.HasValue && DateTimeOffset.UtcNow < token.RotatedAt.Value.Add(RefreshTokenGracePeriod))
        {
            var (newRt, rawRt) = CreateRefreshToken(token.User, token.FamilyId);
            db.RefreshTokens.Add(newRt);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("auth.refresh.race userId:{UserId}", token.UserId);
            metrics.RefreshAttempt("success");
            var newAccess = GenerateAccessToken(token.User);
            return new AuthResult(newAccess.Token, newAccess.ExpiresAt, rawRt, newRt.ExpiresAt);
        }

        // Reuse detected outside grace window — revoke entire token family
        await db.RefreshTokens
            .Where(rt => rt.FamilyId == token.FamilyId && !rt.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true), ct);

        logger.LogWarning("auth.refresh.reuse_detected userId:{UserId} familyId:{FamilyId}", token.UserId, token.FamilyId);
        metrics.RefreshAttempt("reuse_detected");
        throw new UnauthorizedAccessException("Invalid refresh token");
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = HashToken(refreshToken);
        await db.RefreshTokens
            .Where(rt => rt.Token == hash && !rt.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true), ct);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true), ct);
    }

    private AccessToken GenerateAccessToken(User user)
    {
        var expiresOffset = DateTimeOffset.UtcNow.AddMinutes(_jwt.TokenLifetimeMinutes);
        var expires = expiresOffset.UtcDateTime;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: _signingCredentials);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresOffset);
    }

    private (RefreshToken entity, string rawToken) CreateRefreshToken(User user, Guid familyId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = HashToken(rawToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenLifetimeDays),
            FamilyId = familyId
        };
        return (entity, rawToken);
    }

    private static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private class AccessToken(string token, DateTimeOffset expiresAt)
    {
        public string Token { get; set; } = token;
        public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
    }
}
