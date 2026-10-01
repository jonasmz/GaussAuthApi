using System.Text.Json;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Represents an independent consumer: it knows only the JSON contract, never Auth persistence types.
/// </summary>
internal sealed class AuthorizationContextConsumer
{
    private readonly HashSet<string> permissions;

    private AuthorizationContextConsumer(IEnumerable<string> permissions) => this.permissions = new HashSet<string>(permissions, StringComparer.Ordinal);

    public int PermissionCount => permissions.Count;

    public bool Allows(string requiredPermission) => permissions.Contains(requiredPermission);

    public static AuthorizationContextConsumer FromJson(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var permissions = document.RootElement.GetProperty("permissions")
            .EnumerateArray()
            .Select(permission => permission.GetString())
            .OfType<string>();
        return new AuthorizationContextConsumer(permissions);
    }
}
