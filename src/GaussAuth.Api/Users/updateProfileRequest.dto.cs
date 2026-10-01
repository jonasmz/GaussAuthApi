using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GaussAuth.Api.Users;

public sealed class UpdateProfileRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

    [MaxLength(2048)]
    public string? AvatarReference { get; set; }

    /// <summary>
    /// Captures any field not part of this contract (e.g. an attempted <c>email</c>
    /// change) so the endpoint can reject it instead of silently ignoring it (FR-013).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
