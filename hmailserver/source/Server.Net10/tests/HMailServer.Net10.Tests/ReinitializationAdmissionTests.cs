namespace HMailServer.Net10.Tests;

using HMailServer.ComInterop;

[TestClass]
public sealed class ReinitializationAdmissionTests
{
    [TestMethod]
    public void TryAdmit_RunsOneAdmittedAttempt()
    {
        var admission = new ReinitializationAdmission();
        var executionCount = 0;

        Assert.IsTrue(admission.TryAdmit(() => executionCount++));
        Assert.AreEqual(1, executionCount);
    }

    [TestMethod]
    public async Task TryAdmit_DropsDuplicateWhileFirstAttemptIsRunning()
    {
        var admission = new ReinitializationAdmission();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(() => admission.TryAdmit(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
        }));

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var duplicateExecutionCount = 0;
        Assert.IsFalse(admission.TryAdmit(() => duplicateExecutionCount++));

        release.SetResult();
        Assert.IsTrue(await first.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(0, duplicateExecutionCount);
    }

    [TestMethod]
    public void TryAdmit_ReleasesAdmissionWhenAttemptFails()
    {
        var admission = new ReinitializationAdmission();
        var expected = new InvalidOperationException("reinitialization failed");

        var actual = Assert.ThrowsExactly<InvalidOperationException>(
            () => admission.TryAdmit(() => throw expected));

        Assert.AreSame(expected, actual);
        Assert.IsTrue(admission.TryAdmit(static () => { }));
    }
}
