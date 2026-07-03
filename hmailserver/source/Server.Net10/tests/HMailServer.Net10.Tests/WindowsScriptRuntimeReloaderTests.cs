using HMailServer.Core.Abstractions;
using HMailServer.Scripting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WindowsScriptRuntimeReloaderTests
{
    [TestMethod]
    public void Reload_LogsLegacySyntaxErrorEventOnlyForNonEmptyResult()
    {
        var checker = new StubSyntaxChecker(string.Empty);
        var logger = new RecordingLogger<WindowsScriptRuntimeReloader>();
        var reloader = new WindowsScriptRuntimeReloader(checker, logger);

        reloader.Reload("VBScript", @"C:\Events\EventHandlers.vbs");
        Assert.IsEmpty(logger.Entries);

        checker.Result = "File: C:\\Events\\EventHandlers.vbs\r\nSyntax error";
        reloader.Reload("VBScript", @"C:\Events\EventHandlers.vbs");

        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level);
        Assert.AreEqual(5016, logger.Entries[0].EventId.Id);
        StringAssert.Contains(logger.Entries[0].Message, checker.Result);
        Assert.IsNull(logger.Entries[0].Exception);
    }

    [TestMethod]
    public void Reload_ContainsCheckerExceptionAndLogsLegacyLoadErrorEvent()
    {
        var exception = new IOException("syntax checker unavailable");
        var checker = new StubSyntaxChecker(string.Empty) { Exception = exception };
        var logger = new RecordingLogger<WindowsScriptRuntimeReloader>();
        var reloader = new WindowsScriptRuntimeReloader(checker, logger);

        reloader.Reload("JScript", @"C:\Events\EventHandlers.js");

        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level);
        Assert.AreEqual(5017, logger.Entries[0].EventId.Id);
        Assert.AreSame(exception, logger.Entries[0].Exception);
    }

    private sealed class StubSyntaxChecker : IScriptSyntaxChecker
    {
        public StubSyntaxChecker(string result)
        {
            Result = result;
        }

        public string Result { get; set; }

        public Exception? Exception { get; init; }

        public string CheckSyntax(string language, string scriptFile)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            return Result;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new Entry(logLevel, eventId, formatter(state, exception), exception));
    }

    private sealed record Entry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception);
}
