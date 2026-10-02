using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Permissions;

public sealed class UpdatePermissionDescriptionRequest
{
    [MaxLength(500)]
    public string? Description { get; set; }
}
