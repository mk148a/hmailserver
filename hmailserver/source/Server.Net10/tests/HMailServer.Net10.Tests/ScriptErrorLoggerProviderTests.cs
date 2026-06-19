using HMailServer.Core.Abstractions;
using HMailServer.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ScriptErrorLoggerProviderTests
{
    [TestMethod]
    public void Logger_ForwardsWarningErrorAndCriticalWithLegacySeverity()
    {
        var executor = new RecordingErrorEventScriptExecutor();
        using var provider = new ScriptErrorLoggerProvider(executor);
        var logger = provider.CreateLogger("HMailServer.Service.TestWorker");

        logger.LogInformation("ignored");
        logger.LogWarning(new EventId(5014), "warning {Value}", 7);
        logger.LogError(new EventId(5209), new InvalidOperationException("failure"), "error");
        logger.LogCritical(new EventId(5300), "critical");

        Assert.AreEqual(3, executor.Requests.Count);
        Assert.AreEqual(3, executor.Requests[0].Severity);
        Assert.AreEqual(5014, executor.Requests[0].ErrorCode);
        Assert.AreEqual("HMailServer.Service.TestWorker", executor.Requests[0].Source);
        Assert.AreEqual("warning 7", executor.Requests[0].Description);
        Assert.AreEqual(2, executor.Requests[1].Severity);
        StringAssert.Contains(executor.Requests[1].Description, "InvalidOperationException: failure");
        Assert.AreEqual(1, executor.Requests[2].Severity);
    }

    [TestMethod]
    public void Logger_SuppressesRecursiveScriptLogging()
    {
        var executor = new RecordingErrorEventScriptExecutor();
        using var provider = new ScriptErrorLoggerProvider(executor);
        var logger = provider.CreateLogger("HMailServer.Service.TestWorker");
        executor.OnExecute = () => logger.LogError("recursive");

        logger.LogError("outer");

        Assert.AreEqual(1, executor.Requests.Count);
        Assert.AreEqual("outer", executor.Requests[0].Description);
    }

    private sealed class RecordingErrorEventScriptExecutor : IErrorEventScriptExecutor
    {
        public List<ErrorEventScriptExecutionRequest> Requests { get; } = [];

        public Action? OnExecute { get; set; }

        public void Execute(
            ErrorEventScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            OnExecute?.Invoke();
        }
    }
}
