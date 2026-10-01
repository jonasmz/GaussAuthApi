using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Permissions;

public sealed class CreatePermissionRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }
}
