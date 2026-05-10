using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(UserDto dto, string ip);
        Task<AuthResult> LoginAsync(UserDto dto, string ip);
        Task<AuthResult> RefreshAsync(string refreshToken, string ip);
        Task LogoutAsync(Guid userId);
    }
}
