using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ServerStatusRuntimeStateTests
{
    [TestMethod]
    public void Capture_ExposesLegacyCountersAndActiveSessionCounts()
    {
        var startedAt = new DateTimeOffset(new DateTime(2026, 6, 28, 1, 2, 3, DateTimeKind.Local));
        var state = new ServerStatusRuntimeState(startedAt);

        state.OnMessageProcessed();
        state.OnMessageProcessed();
        state.OnVirusRemoved();
        state.OnSpamMessageDetected();
        state.OnSpamMessageDetected();
        using var smtpSession = state.TrackSession(1);
        using var pop3Session = state.TrackSession(3);
        using var secondPop3Session = state.TrackSession(3);

        var snapshot = state.Capture();

        Assert.AreEqual("2026-06-28 01:02:03", snapshot.StartTime);
        Assert.AreEqual(2, snapshot.ProcessedMessages);
        Assert.AreEqual(1, snapshot.RemovedViruses);
        Assert.AreEqual(2, snapshot.RemovedSpamMessages);
        Assert.AreEqual(1, snapshot.SessionCounts[1]);
        Assert.AreEqual(2, snapshot.SessionCounts[3]);
        Assert.IsGreaterThan(0, snapshot.ThreadID);

        pop3Session.Dispose();
        secondPop3Session.Dispose();

        snapshot = state.Capture();

        Assert.AreEqual(1, snapshot.SessionCounts[1]);
        Assert.AreEqual(0, snapshot.SessionCounts[3]);
    }
}
