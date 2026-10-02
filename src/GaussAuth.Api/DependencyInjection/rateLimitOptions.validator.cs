using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Api.DependencyInjection;

public static class RateLimitOptionsValidator
{
    public const int MaximumPermitLimit = 10_000_000;
    public const int MaximumWindowSeconds = 86_400;

    /// <summary>Reads one group, applying the defaults when the keys are absent and rejecting impossible values.</summary>
    public static RateLimitPolicyOptions Read(IConfiguration configuration, string group, int defaultPermitLimit, int defaultWindowSeconds)
    {
        var permitLimit = configuration.GetBoundedInt32(
            $"{RateLimitOptions.SectionName}:{group}:PermitLimit", defaultPermitLimit, 1, MaximumPermitLimit);
        var windowSeconds = configuration.GetBoundedInt32(
            $"{RateLimitOptions.SectionName}:{group}:WindowSeconds", defaultWindowSeconds, 1, MaximumWindowSeconds);
        return new RateLimitPolicyOptions(permitLimit, windowSeconds);
    }
}
