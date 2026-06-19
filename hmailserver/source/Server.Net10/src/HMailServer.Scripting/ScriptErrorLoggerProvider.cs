using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace HMailServer.Scripting;

public sealed class ScriptErrorLoggerProvider : ILoggerProvider
{
    private readonly IErrorEventScriptExecutor _executor;
    private readonly AsyncLocal<int> _dispatchDepth = new();
    private bool _disposed;

    public ScriptErrorLoggerProvider(IErrorEventScriptExecutor executor)
    {
        _executor = executor;
    }

    public ILogger CreateLogger(string categoryName) =>
        new ScriptErrorLogger(this, categoryName);

    public void Dispose()
    {
        _disposed = true;
    }

    private bool IsEnabled(LogLevel logLevel) =>
        !_disposed && logLevel is LogLevel.Warning or LogLevel.Error or LogLevel.Critical;

    private void Dispatch<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || _dispatchDepth.Value > 0)
        {
            return;
        }

        _dispatchDepth.Value++;
        try
        {
            var description = formatter(state, exception) ?? string.Empty;
            if (exception is not null)
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? exception.ToString()
                    : description + Environment.NewLine + exception;
            }

            _executor.Execute(
                new ErrorEventScriptExecutionRequest(
                    ToLegacySeverity(logLevel),
                    eventId.Id,
                    categoryName,
                    description),
                CancellationToken.None);
        }
        catch
        {
        }
        finally
        {
            _dispatchDepth.Value--;
        }
    }

    private static int ToLegacySeverity(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Critical => 1,
            LogLevel.Error => 2,
            _ => 3
        };

    private sealed class ScriptErrorLogger : ILogger
    {
        private readonly ScriptErrorLoggerProvider _provider;
        private readonly string _categoryName;

        public ScriptErrorLogger(
            ScriptErrorLoggerProvider provider,
            string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            _provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _provider.Dispatch(
                _categoryName,
                logLevel,
                eventId,
                state,
                exception,
                formatter);
        }
    }
}
