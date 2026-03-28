using System.ComponentModel.DataAnnotations;

namespace Pdmt.Api.Dto;

public class CreateTagDto
{
    [Required, MaxLength(100)]
    public required string Name { get; set; }
}
