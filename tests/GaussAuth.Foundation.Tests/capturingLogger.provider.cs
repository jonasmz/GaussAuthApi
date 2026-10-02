using Microsoft.Extensions.Logging;

namespace GaussAuth.Foundation.Tests;

/// <summary>Collects every formatted log message and exception text so tests can prove a value was never logged.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> entries = [];

    public IReadOnlyList<string> Entries { get { lock (entries) return entries.ToArray(); } }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose() { }

    private void Add(string entry) { lock (entries) entries.Add(entry); }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Add(formatter(state, exception) + (exception?.ToString() ?? string.Empty));
    }
}
