using Microsoft.Extensions.Configuration;

namespace GaussAuth.Api.DependencyInjection;

/// <summary>
/// Validated per-group rate limits (<c>RateLimiting:&lt;Group&gt;:PermitLimit</c> / <c>WindowSeconds</c>). Each group has its
/// own default because the groups have different abuse profiles; impossible values fail startup naming the key.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public required RateLimitPolicyOptions UserCreation { get; init; }
    public required RateLimitPolicyOptions Login { get; init; }
    public required RateLimitPolicyOptions SigningKeys { get; init; }
    public required RateLimitPolicyOptions SessionCredentials { get; init; }
    public required RateLimitPolicyOptions PasswordRecovery { get; init; }
    public required RateLimitPolicyOptions PasswordReset { get; init; }
    public required RateLimitPolicyOptions AuthorizationContext { get; init; }
    public required RateLimitPolicyOptions SecurityEvents { get; init; }
    public required RateLimitPolicyOptions Administration { get; init; }
    public required RateLimitPolicyOptions ProfileImageWrite { get; init; }

    public static RateLimitOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new RateLimitOptions
        {
            UserCreation = RateLimitOptionsValidator.Read(configuration, "UserCreation", 5, 60),
            Login = RateLimitOptionsValidator.Read(configuration, "Login", 5, 60),
            SigningKeys = RateLimitOptionsValidator.Read(configuration, "SigningKeys", 60, 60),
            SessionCredentials = RateLimitOptionsValidator.Read(configuration, "SessionCredentials", 600, 60),
            PasswordRecovery = RateLimitOptionsValidator.Read(configuration, "PasswordRecovery", 5, 60),
            PasswordReset = RateLimitOptionsValidator.Read(configuration, "PasswordReset", 5, 60),
            AuthorizationContext = RateLimitOptionsValidator.Read(configuration, "AuthorizationContext", 600, 60),
            SecurityEvents = RateLimitOptionsValidator.Read(configuration, "SecurityEvents", 60, 60),
            Administration = RateLimitOptionsValidator.Read(configuration, "Administration", 120, 60),
            ProfileImageWrite = RateLimitOptionsValidator.Read(configuration, "ProfileImageWrite", 10, 60)
        };
    }
}
