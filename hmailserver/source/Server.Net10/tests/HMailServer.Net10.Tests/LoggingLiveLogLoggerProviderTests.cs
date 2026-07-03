using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LoggingLiveLogLoggerProviderTests
{
    [TestMethod]
    public void Logger_DisabledRuntimeSkipsFormattingAndAppending()
    {
        var runtime = new ProcessLocalLoggingLiveLogRuntime();
        using var provider = new LoggingLiveLogLoggerProvider(runtime);
        var logger = provider.CreateLogger("Test.Category");
        var formatterCalls = 0;

        logger.Log(
            LogLevel.Information,
            new EventId(1),
            "ignored",
            exception: null,
            (state, _) =>
            {
                formatterCalls++;
                return state;
            });

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.AreEqual(0, formatterCalls);
        Assert.AreEqual(string.Empty, runtime.ReadAndClear());
    }

    [TestMethod]
    public void Logger_EnabledThroughComWritesDeterministicRecordReadableThroughCom()
    {
        var runtime = new ProcessLocalLoggingLiveLogRuntime();
        using var provider = new LoggingLiveLogLoggerProvider(runtime);
        var logger = provider.CreateLogger("Test.Category");
        IInterfaceLogging logging = Logging.CreateAuthorized(
            new LoggingAdministrationSnapshot(
                LoggingMask: 0,
                Device: 0,
                LogFormat: 0,
                AwStatsEnabled: false,
                Directory: string.Empty),
            liveLogRuntime: runtime);

        logging.EnableLiveLogging(true);
        Assert.IsFalse(logger.IsEnabled(LogLevel.None));
        logger.LogInformation("Processed message {MessageId}.", 42);

        Assert.AreEqual(
            "Information\tTest.Category\tProcessed message 42.\r\n",
            logging.LiveLog);
        Assert.AreEqual(string.Empty, logging.LiveLog);
        Assert.IsTrue(logging.LiveLoggingEnabled);
    }

    [TestMethod]
    public void Logger_NormalizesMessageAndExceptionLineEndings()
    {
        var runtime = new ProcessLocalLoggingLiveLogRuntime();
        using var provider = new LoggingLiveLogLoggerProvider(runtime);
        var logger = provider.CreateLogger("Error.Category");
        var exception = new InvalidOperationException("first\nsecond");
        runtime.Enable(true);

        logger.LogError(exception, "line one\nline two");
        var result = runtime.ReadAndClear();

        StringAssert.StartsWith(
            result,
            "Error\tError.Category\tline one\r\nline two\r\nSystem.InvalidOperationException: first\r\nsecond");
        StringAssert.EndsWith(result, "\r\n");
        Assert.IsFalse(result.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\n'));
    }

    [TestMethod]
    public void DisposedProviderStopsFutureWritesWithoutChangingRuntimeState()
    {
        var runtime = new ProcessLocalLoggingLiveLogRuntime();
        var provider = new LoggingLiveLogLoggerProvider(runtime);
        var logger = provider.CreateLogger("Test.Category");
        runtime.Enable(true);

        provider.Dispose();
        logger.LogWarning("ignored");

        Assert.IsFalse(logger.IsEnabled(LogLevel.Warning));
        Assert.IsTrue(runtime.Enabled);
        Assert.AreEqual(string.Empty, runtime.ReadAndClear());
    }
}
