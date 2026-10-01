using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Passwords;

public sealed class PasswordRecoveryRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
}
