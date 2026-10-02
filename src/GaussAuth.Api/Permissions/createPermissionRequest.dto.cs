using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Permissions;

public sealed class CreatePermissionRequest
{
    [Required, MaxLength(128)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
