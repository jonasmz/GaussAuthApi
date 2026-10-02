namespace GaussAuth.Api.Commands;

/// <summary>
/// Dispatches one-off operator subcommands (<c>migrate</c>, <c>bootstrap-admin</c>) that run in a minimal host and
/// exit, instead of starting the web host. With no subcommand the caller proceeds to the normal web host unchanged.
/// A bare word that is not a known subcommand is rejected so a mistyped command can never silently start the server.
/// </summary>
public static class CommandLineCommands
{
    public const int UnknownCommandExitCode = 64;
    public const int UnavailableCommandExitCode = 70;

    private delegate Task<int> CommandHandler(string[] arguments);

    // Subcommands reserved by the release contract. Handlers are registered here as they are implemented.
    private static readonly Dictionary<string, CommandHandler> Handlers = new(StringComparer.Ordinal)
    {
        ["migrate"] = MigrateCommand.RunAsync,
        ["bootstrap-admin"] = BootstrapAdministratorCommand.RunAsync
    };

    private static readonly string[] ReservedCommands = ["migrate", "bootstrap-admin"];

    /// <summary>
    /// Returns the process exit code when a subcommand was recognized and executed, or <c>null</c> when the arguments
    /// contain no subcommand and the web host should start.
    /// </summary>
    public static async Task<int?> TryRunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0].StartsWith('-')) return null;

        var name = args[0];
        if (Handlers.TryGetValue(name, out var handler)) return await handler(args[1..]);

        await Console.Error.WriteLineAsync(
            $"Unknown command '{name}'. Supported commands: {string.Join(", ", ReservedCommands)}. Run without arguments to start the API.");
        return UnknownCommandExitCode;
    }
}
