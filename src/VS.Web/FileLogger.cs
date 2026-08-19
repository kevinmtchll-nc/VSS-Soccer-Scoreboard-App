using Microsoft.Extensions.Logging;

namespace VS.Web;

public sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object _sync = new();

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, path, _sync);

    public void Dispose() { }
}

internal sealed class FileLogger(
    string categoryName,
    string path,
    object sync) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var line =
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
            $"[{logLevel}] {categoryName}: {formatter(state, exception)}";

        if (exception is not null)
            line += Environment.NewLine + exception;

        lock (sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
