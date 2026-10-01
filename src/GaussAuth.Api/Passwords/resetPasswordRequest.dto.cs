using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Passwords;

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(4096)] public string RecoveryCredential { get; set; } = string.Empty;
    [Required, MaxLength(128)] public string NewPassword { get; set; } = string.Empty;
}
