using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(UserDto dto, string ip, CancellationToken ct);
        Task<AuthResult> LoginAsync(UserDto dto, string ip, CancellationToken ct);
        Task<AuthResult> RefreshAsync(string refreshToken, string ip, CancellationToken ct);
        Task LogoutAsync(string refreshToken, CancellationToken ct);
        Task LogoutAllAsync(Guid userId, CancellationToken ct);
    }
}
