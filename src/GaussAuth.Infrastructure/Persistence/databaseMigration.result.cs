namespace GaussAuth.Infrastructure.Persistence;

/// <summary>
/// Outcome of applying migrations. A failure carries only a category, the failing migration id when known, and a safe
/// failure code (a PostgreSQL SQLSTATE or an exception type name) — never a connection string, credential, message or
/// stack trace.
/// </summary>
public sealed record DatabaseMigrationResult(
    bool Succeeded,
    IReadOnlyList<string> AppliedMigrations,
    string? FailureCategory = null,
    string? FailedMigration = null,
    string? FailureCode = null)
{
    public const string ConnectionCategory = "connection";
    public const string AuthenticationCategory = "authentication";
    public const string MigrationFailedCategory = "migration-failed";
    public const string CancelledCategory = "cancelled";
    public const string LockedCategory = "locked";

    public static DatabaseMigrationResult Success(IReadOnlyList<string> applied) => new(true, applied);

    public static DatabaseMigrationResult Failure(IReadOnlyList<string> applied, string category, string? failedMigration, string? failureCode) =>
        new(false, applied, category, failedMigration, failureCode);
}
