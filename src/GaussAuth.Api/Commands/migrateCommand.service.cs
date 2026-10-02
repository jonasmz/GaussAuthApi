using System.Runtime.InteropServices;
using GaussAuth.Api.Startup;
using GaussAuth.Infrastructure.Configuration;
using GaussAuth.Infrastructure.DependencyInjection;
using GaussAuth.Infrastructure.Persistence;

namespace GaussAuth.Api.Commands;

/// <summary>
/// <c>migrate</c>: applies pending database migrations in a minimal host and exits. It needs only
/// <c>ConnectionStrings:AuthenticationDatabase</c> (no signing key, storage or other API settings). The web host never
/// applies migrations; operators run this command (or the equivalent SQL script) as an explicit deployment step.
/// Failures are reported as one structured Critical entry with a category, the failing migration and a safe code only.
/// </summary>
public static class MigrateCommand
{
    public const int FailureExitCode = 1;

    public static async Task<int> RunAsync(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(arguments);
        // EF Core logs the failing SQL and exception (with stack trace) on errors; the command reports its own safe summary.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

        string connection;
        try
        {
            connection = AuthenticationDatabaseConnection.Read(builder.Configuration);
        }
        catch (StartupConfigurationException exception)
        {
            return StartupFailureReporter.Report(exception);
        }

        builder.Services.AddAuthenticationPersistence(connection);
        builder.Services.AddScoped<DatabaseMigrator>();
        using var host = builder.Build();

        using var cancellation = new CancellationTokenSource();
        using var termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GaussAuth.Migrate");
        using var scope = host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellation.Token);

        if (result.Succeeded) return 0;

        logger.LogCritical(
            "Migration failed. Category {Category}, migration {Migration}, code {Code}. {Hint} The deployment must stop; do not start a version that expects a newer schema.",
            result.FailureCategory,
            result.FailedMigration ?? "n/a",
            result.FailureCode ?? "n/a",
            HintFor(result.FailureCategory));
        return FailureExitCode;
    }

    // Fixed, environment-free guidance per category: actionable without echoing hosts, users or any configured value.
    private static string HintFor(string? category) => category switch
    {
        DatabaseMigrationResult.ConnectionCategory =>
            "Check that PostgreSQL is running and reachable from this container, and the host, port and database in the connection string.",
        DatabaseMigrationResult.AuthenticationCategory =>
            "Check the database user and password supplied through ConnectionStrings:AuthenticationDatabase.",
        DatabaseMigrationResult.MigrationFailedCategory =>
            "The failed migration was rolled back. Inspect the database for objects or data that conflict with it, correct them, and run the command again.",
        DatabaseMigrationResult.LockedCategory =>
            "Another migration run is holding the migration lock. Wait for it to finish, or stop it, and run the command again.",
        DatabaseMigrationResult.CancelledCategory =>
            "The command was interrupted; the interrupted migration was rolled back. Run the command again.",
        _ => "See the category above."
    };
}
