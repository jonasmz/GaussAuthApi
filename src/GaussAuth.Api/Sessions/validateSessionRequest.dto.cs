using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Sessions;

public sealed class ValidateSessionRequest
{
    [Required, MaxLength(64)]
    public string ApplicationCode { get; set; } = string.Empty;
}
