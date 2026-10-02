using Microsoft.Extensions.Hosting;

namespace GaussAuth.Infrastructure.Configuration;

public static class ProductionClassEnvironmentExtensions
{
    /// <summary>
    /// True for every environment other than <c>Development</c> and <c>Testing</c>. Unsafe development conveniences
    /// are reachable only outside this class, so an unknown or mistyped environment name (for example
    /// <c>Staging</c>) is treated as production rather than silently receiving them.
    /// </summary>
    public static bool IsProductionClass(this IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
    }
}
