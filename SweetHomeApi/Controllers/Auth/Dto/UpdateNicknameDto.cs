using System.ComponentModel.DataAnnotations;

namespace SweetHomeApi.Controllers.Auth.Dto;

public class UpdateNicknameDto
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;
}
