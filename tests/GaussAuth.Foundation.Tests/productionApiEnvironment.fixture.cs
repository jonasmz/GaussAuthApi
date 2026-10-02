using System.Security.Cryptography;

namespace GaussAuth.Foundation.Tests;

/// <summary>A complete, valid environment-variable set for starting the API process in a given environment.</summary>
internal static class ProductionApiEnvironment
{
    public const string DefaultConnection = "Host=localhost;Database=foundation_test;Username=test;Password=not_a_secret";

    private static readonly string SigningKeyPem = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem();

    /// <summary>
    /// Every setting a Production-class environment requires, listening on a random loopback port. Entries set to null
    /// remove inherited variables so the child sees only what the test intends.
    /// </summary>
    public static Dictionary<string, string?> Create(string environmentName, string? connection = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "gaussauth-startup-validation");
        return new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",
            ["ConnectionStrings__AuthenticationDatabase"] = connection ?? DefaultConnection,
            ["Sessions__Signing__PrivateKeyPem"] = SigningKeyPem,
            ["Sessions__Signing__PrivateKeyPemFile"] = null,
            ["ProfileImages__RootPath"] = Path.Combine(root, "images"),
            ["ProfileImages__StorageIsPersistent"] = "true",
            ["DataProtection__KeysPath"] = Path.Combine(root, "keys"),
            ["PasswordRecovery__DeliveryFile"] = null,
            ["SecurityAudit__GlobalReviewerUserId"] = null,
            ["Administration__GlobalAdministratorUserIds__0"] = null
        };
    }
}
