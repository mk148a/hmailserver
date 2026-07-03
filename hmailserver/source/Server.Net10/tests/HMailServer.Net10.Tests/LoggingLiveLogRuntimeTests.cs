using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LoggingLiveLogRuntimeTests
{
    [TestMethod]
    public void Append_AtLegacyLimitRemainsEnabledAndReadable()
    {
        var runtime = new ProcessLocalLoggingLiveLogRuntime();
        var content = new string('x', ProcessLocalLoggingLiveLogRuntime.LegacyMaximumLength);

        runtime.Enable(true);
        runtime.Append(content);

        Assert.IsTrue(runtime.Enabled);
        Assert.AreEqual(content, runtime.ReadAndClear());
        Assert.AreEqual(string.Empty, runtime.ReadAndClear());
    }

    [TestMethod]
    public void Append_OverLegacyLimitClearsBufferAndDisablesLiveLogging()
    {
        var runtime = new ProcessLocalLoggingLiveLogRuntime();

        runtime.Enable(true);
        runtime.Append(new string('x', ProcessLocalLoggingLiveLogRuntime.LegacyMaximumLength + 1));

        Assert.IsFalse(runtime.Enabled);
        Assert.AreEqual(string.Empty, runtime.ReadAndClear());
        runtime.Append("ignored while disabled");
        Assert.AreEqual(string.Empty, runtime.ReadAndClear());
    }
}
