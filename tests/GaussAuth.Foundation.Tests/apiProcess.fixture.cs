using System.Diagnostics;
using System.Text;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Launches the built API assembly as a separate process so tests can observe real startup behavior: exit codes,
/// console output and the absence of secrets or stack traces. The child inherits the test process environment;
/// entries in <c>environment</c> override it and a <c>null</c> value removes the variable.
/// </summary>
internal static class ApiProcess
{
    public static async Task<(int ExitCode, string Output, bool TimedOut)> RunAsync(
        IReadOnlyDictionary<string, string?> environment,
        TimeSpan timeout,
        params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var (key, value) in environment)
        {
            if (value is null) start.Environment.Remove(key);
            else start.Environment[key] = value;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start API process.");
        var output = new StringBuilder();
        var sync = new object();
        void Capture(string? line)
        {
            if (line is null) return;
            lock (sync) output.AppendLine(line);
        }

        process.OutputDataReceived += (_, e) => Capture(e.Data);
        process.ErrorDataReceived += (_, e) => Capture(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cancellation = new CancellationTokenSource(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            // Drain asynchronous output handlers after exit.
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        string captured;
        lock (sync) captured = output.ToString();
        return (timedOut ? -1 : process.ExitCode, captured, timedOut);
    }
}
