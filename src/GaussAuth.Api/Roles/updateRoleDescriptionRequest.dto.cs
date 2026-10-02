using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Roles;

public sealed class UpdateRoleDescriptionRequest
{
    [MaxLength(500)]
    public string? Description { get; set; }
}
