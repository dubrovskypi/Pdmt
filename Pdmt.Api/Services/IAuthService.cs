using Pdmt.Api.Dto;

namespace Pdmt.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResultDto> RegisterAsync(UserDto dto);
        Task<AuthResultDto> LoginAsync(UserDto dto);
        Task<AuthResultDto> RefreshAsync(string refreshToken);
        Task LogoutAsync(Guid userId);
    }
}
