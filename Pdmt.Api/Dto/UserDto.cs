using System.ComponentModel.DataAnnotations;

namespace Pdmt.Api.Dto
{
    public class UserDto
    {
        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = null!;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = null!;
    }
}
