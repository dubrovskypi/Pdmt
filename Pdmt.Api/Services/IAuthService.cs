using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResultDto> RegisterAsync(UserDto dto, string ip);
        Task<AuthResultDto> LoginAsync(UserDto dto, string ip);
        Task<AuthResultDto> RefreshAsync(string refreshToken, string ip);
        Task LogoutAsync(Guid userId);
    }
}
