namespace GaussAuth.Application.Administration.Authorization;

/// <summary>
/// The single, stable catalog of Auth administrative permission codes. All codes carry the reserved <c>auth.</c> prefix.
/// Application-scoped codes are seeded into every Application; global codes are reserved and documented only, because
/// those operations are governed by the global administrator capability.
/// </summary>
public static class AdministrativePermissionCatalog
{
    public const string ReservedPrefix = "auth.";

    public const string MembershipsRead = "auth.memberships.read";
    public const string MembershipsManage = "auth.memberships.manage";
    public const string RolesRead = "auth.roles.read";
    public const string RolesManage = "auth.roles.manage";
    public const string PermissionsRead = "auth.permissions.read";
    public const string PermissionsManage = "auth.permissions.manage";
    public const string SessionsRead = "auth.sessions.read";
    public const string SessionsRevoke = "auth.sessions.revoke";
    public const string SecurityAuditRead = "auth.security.audit.read";

    public const string UsersRead = "auth.users.read";
    public const string UsersManage = "auth.users.manage";
    public const string ApplicationsRead = "auth.applications.read";
    public const string ApplicationsManage = "auth.applications.manage";
    public const string ConsumerSecretsRotate = "auth.consumer-secrets.rotate";

    private static readonly IReadOnlyDictionary<string, string> SeededDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [MembershipsRead] = "Read application memberships through Auth administration.",
        [MembershipsManage] = "Create, activate, and deactivate application memberships through Auth administration.",
        [RolesRead] = "Read roles, user-role assignments, and effective authorization through Auth administration.",
        [RolesManage] = "Manage roles and user-role assignments through Auth administration.",
        [PermissionsRead] = "Read permissions and role-permission assignments through Auth administration.",
        [PermissionsManage] = "Manage permissions and role-permission assignments through Auth administration.",
        [SessionsRead] = "Read application sessions through Auth administration.",
        [SessionsRevoke] = "Revoke application sessions through Auth administration.",
        [SecurityAuditRead] = "Read this application's security audit events.",
    };

    private static readonly IReadOnlySet<string> GlobalOnlyCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        UsersRead, UsersManage, ApplicationsRead, ApplicationsManage, ConsumerSecretsRotate,
    };

    /// <summary>Codes seeded into every Application, in a stable order, with their descriptions.</summary>
    public static IReadOnlyList<(string Code, string Description)> SeededPermissions { get; } =
        SeededDescriptions.Select(entry => (entry.Key, entry.Value)).ToArray();

    public static bool IsReservedPrefix(string? code) =>
        code is not null && code.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsPlatformPermission(string? code) =>
        code is not null && (SeededDescriptions.ContainsKey(code.ToLowerInvariant()) || GlobalOnlyCodes.Contains(code.ToLowerInvariant()));
}
