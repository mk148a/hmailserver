using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed class LoggingLiveLogLoggerProvider : ILoggerProvider
{
    private readonly ILoggingLiveLogRuntime _runtime;
    private bool _disposed;

    public LoggingLiveLogLoggerProvider()
        : this(LoggingLiveLogRuntimeHost.Current)
    {
    }

    public LoggingLiveLogLoggerProvider(ILoggingLiveLogRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new LiveLogLogger(this, categoryName);
    }

    public void Dispose() => _disposed = true;

    private bool IsEnabled() => !_disposed && _runtime.Enabled;

    private void Append<TState>(
        string categoryName,
        LogLevel logLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled())
        {
            return;
        }

        var message = formatter(state, exception) ?? string.Empty;
        var record = $"{logLevel}\t{categoryName}\t{message}";
        if (exception is not null)
        {
            record += "\r\n" + exception;
        }

        _runtime.Append(NormalizeLineEndings(record).TrimEnd('\r', '\n') + "\r\n");
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);

    private sealed class LiveLogLogger : ILogger
    {
        private readonly LoggingLiveLogLoggerProvider _provider;
        private readonly string _categoryName;

        public LiveLogLogger(LoggingLiveLogLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && _provider.IsEnabled();

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _provider.Append(_categoryName, logLevel, state, exception, formatter);
        }
    }
}
