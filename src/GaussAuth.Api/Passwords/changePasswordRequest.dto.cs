using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Passwords;

public sealed class ChangePasswordRequest
{
    [Required, MaxLength(128)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MaxLength(128)] public string NewPassword { get; set; } = string.Empty;
}
