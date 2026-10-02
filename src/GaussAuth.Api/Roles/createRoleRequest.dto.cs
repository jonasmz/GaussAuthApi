using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Roles;

public sealed class CreateRoleRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
