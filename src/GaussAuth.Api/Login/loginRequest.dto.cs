using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Login;

public sealed class LoginRequest
{
    [Required, MaxLength(64)]
    public string ApplicationCode { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
