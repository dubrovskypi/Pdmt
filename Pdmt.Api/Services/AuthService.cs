using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Services;

public class AuthService(AppDbContext db, IConfiguration config, IRateLimitService rateLimit, SigningCredentials signingCreds) : IAuthService
{
    private readonly SigningCredentials _signingCredentials = signingCreds;

    public async Task<AuthResultDto> RegisterAsync(UserDto dto, string ip)
    {
        await rateLimit.CheckAsync("Auth.Register", ip);

        var normalizedEmail = dto.Email.Trim().ToLower();
        var exists = await db.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (exists)
            throw new InvalidOperationException("User already exists");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (refreshTokenEntity, rawRefreshToken) = CreateRefreshToken(user);

        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync();

        var accessToken = GenerateAccessToken(user);
        return new AuthResultDto
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = rawRefreshToken
        };
    }

    public async Task<AuthResultDto> LoginAsync(UserDto dto, string ip)
    {
        await rateLimit.CheckAsync("Auth.Login", ip);

        var normalizedEmail = dto.Email.Trim().ToLower();
        var user = await db.Users.
            Include(u => u.RefreshTokens).
            FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            db.FailedLoginAttempts.Add(new FailedLoginAttempt
            {
                Email = normalizedEmail,
                IpAddress = ip,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Reason = "Invalid credentials"
            });
            await db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // revoke old tokens
        foreach (var rt in user.RefreshTokens)
            rt.IsRevoked = true;

        var (refreshTokenEntity, rawRefreshToken) = CreateRefreshToken(user);

        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync();

        var accessToken = GenerateAccessToken(user);
        return new AuthResultDto
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = rawRefreshToken
        };
    }

    public async Task<AuthResultDto> RefreshAsync(string refreshToken, string ip)
    {
        await rateLimit.CheckAsync("Auth.Refresh", ip);

        var hashedRefreshToken = HashToken(refreshToken);
        var token = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt =>
                rt.Token == hashedRefreshToken &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTimeOffset.UtcNow) ?? throw new UnauthorizedAccessException("Invalid refresh token");
        token.IsRevoked = true;

        var (newRefreshTokenEntity, rawRefreshToken) = CreateRefreshToken(token.User);
        db.RefreshTokens.Add(newRefreshTokenEntity);
        await db.SaveChangesAsync();

        var accessToken = GenerateAccessToken(token.User);
        return new AuthResultDto
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = rawRefreshToken
        };
    }

    public async Task LogoutAsync(Guid userId)
    {
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
            token.IsRevoked = true;

        await db.SaveChangesAsync();
    }

    private AccessToken GenerateAccessToken(User user)
    {
        var jwt = config.GetSection("Jwt");
        var expiresOffset = DateTimeOffset.UtcNow.AddMinutes(int.Parse(jwt["TokenLifetimeMinutes"]!));
        var expires = expiresOffset.UtcDateTime;
        var claims = new[]
        {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: _signingCredentials);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresOffset);
    }

    private (RefreshToken entity, string rawToken) CreateRefreshToken(User user)
    {
        var days = int.Parse(config["Jwt:RefreshTokenLifetimeDays"]!);
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = HashToken(rawToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(days)
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
