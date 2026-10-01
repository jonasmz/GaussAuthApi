using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Roles;

public sealed class CreateRoleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
