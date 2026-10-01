using GaussAuth.Application.Administration.Ports;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.Administration;

public sealed class ConfiguredGlobalAdministratorPolicy : IGlobalAdministratorPolicy
{
    private const string ConfigurationKey = "Administration:GlobalAdministratorUserIds";
    private const string LegacyConfigurationKey = "SecurityAudit:GlobalReviewerUserId";
    private readonly HashSet<Guid> globalAdministrators;

    public ConfiguredGlobalAdministratorPolicy(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration[LegacyConfigurationKey]))
            throw new InvalidOperationException($"{LegacyConfigurationKey} is no longer supported; configure {ConfigurationKey} instead.");

        var administrators = new HashSet<Guid>();
        foreach (var entry in configuration.GetSection(ConfigurationKey).GetChildren())
        {
            // A blank entry means "not configured" (for example an unset deployment variable) and confers no authority.
            if (string.IsNullOrWhiteSpace(entry.Value)) continue;
            if (!Guid.TryParse(entry.Value, out var userId) || userId == Guid.Empty)
                throw new InvalidOperationException("Global administrator configuration is invalid.");
            administrators.Add(userId);
        }

        globalAdministrators = administrators;
    }

    public bool IsGlobalAdministrator(Guid userId) => globalAdministrators.Contains(userId);
}
