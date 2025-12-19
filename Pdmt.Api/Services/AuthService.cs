using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IRateLimitService _rateLimit;

        public AuthService(AppDbContext db, IConfiguration config, IRateLimitService rateLimit)
        {
            _db = db;
            _config = config;
            _rateLimit = rateLimit;
        }

        public async Task<AuthResultDto> RegisterAsync(UserDto dto, string ip)
        {
            await _rateLimit.CheckAsync("Auth.Register", ip);

            var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
            if (exists)
                throw new InvalidOperationException("User already exists");
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var refreshToken = CreateRefreshToken(user);

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);
            return new AuthResultDto
            {
                AccessToken = accessToken.Token,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task<AuthResultDto> LoginAsync(UserDto dto)
        {
            await _rateLimit.CheckAsync("Auth.Login", dto.Email);

            var user = await _db.Users.
                Include(u => u.RefreshTokens).
                FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
                throw new InvalidOperationException("Invalid credentials (un)");
            var ok = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!ok)
                throw new InvalidOperationException("Invalid credentials (pwd)");

            // revoke old tokens
            foreach (var rt in user.RefreshTokens)
                rt.IsRevoked = true;

            var refreshToken = CreateRefreshToken(user);

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);
            return new AuthResultDto
            {
                AccessToken = accessToken.Token,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task<AuthResultDto> RefreshAsync(string refreshToken)
        {
            //todo hash refresh token
            await _rateLimit.CheckAsync("Auth.Refresh", refreshToken);

            var token = await _db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt =>
                    rt.Token == refreshToken &&
                    !rt.IsRevoked &&
                    rt.ExpiresAt > DateTime.UtcNow);

            if (token == null)
                throw new InvalidOperationException("Invalid refresh token");

            token.IsRevoked = true;

            var newRefreshToken = CreateRefreshToken(token.User);
            _db.RefreshTokens.Add(newRefreshToken);
            await _db.SaveChangesAsync();

            var accessToken = GenerateAccessToken(token.User);
            return new AuthResultDto
            {
                AccessToken = accessToken.Token,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = newRefreshToken.Token
            };
        }

        public async Task LogoutAsync(Guid userId)
        {
            var tokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
                token.IsRevoked = true;

            await _db.SaveChangesAsync();
        }

        private AccessToken GenerateAccessToken(User user)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["TokenLifetimeMinutes"]!));
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
                signingCredentials: creds);
            return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        private RefreshToken CreateRefreshToken(User user)
        {
            var days = int.Parse(_config["Jwt:RefreshTokenLifetimeDays"]!);
            return new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(days)
            };
        }

        private class AccessToken
        {
            public string Token { get; set; } = null!;
            public DateTime ExpiresAt { get; set; }
            public AccessToken(string token, DateTime expiresAt)
            {
                Token = token;
                ExpiresAt = expiresAt;
            }
        }
    }
}
