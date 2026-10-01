using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Applications;

public sealed class CreateApplicationRequest
{
    [Required, MaxLength(64)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
}
