using System.Runtime.InteropServices;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSessionBrokerCallerGuardTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const string WorkerSid = "S-1-5-82-2759919546-3181318411-3457700337-2112356574-3667061494";

    [TestMethod]
    public void AuthorizedIdentificationTokenRunsOperationAndReverts()
    {
        var source = new FakeIdentitySource(new(
            WorkerSid,
            WebAdminBrokerTokenType.Impersonation,
            WebAdminBrokerImpersonationLevel.Identification,
            IsRemote: false));
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);
        var calls = 0;

        var result = guard.Invoke(() =>
        {
            calls++;
            return "authorized";
        });

        Assert.AreEqual("authorized", result);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, source.RevertCalls);
    }

    [TestMethod]
    [DataRow(null, WebAdminBrokerTokenType.Unknown, WebAdminBrokerImpersonationLevel.Anonymous, false)]
    [DataRow(WorkerSid, WebAdminBrokerTokenType.Primary, WebAdminBrokerImpersonationLevel.Impersonation, false)]
    [DataRow(WorkerSid, WebAdminBrokerTokenType.Impersonation, WebAdminBrokerImpersonationLevel.Anonymous, false)]
    [DataRow("S-1-5-21-901680329-682942131-322962695-1002", WebAdminBrokerTokenType.Impersonation, WebAdminBrokerImpersonationLevel.Identification, false)]
    [DataRow(WorkerSid, WebAdminBrokerTokenType.Impersonation, WebAdminBrokerImpersonationLevel.Identification, true)]
    public void MissingAnonymousPrimaryWeakMismatchedAndRemoteCallersAreDenied(
        string? sid,
        WebAdminBrokerTokenType tokenType,
        WebAdminBrokerImpersonationLevel impersonationLevel,
        bool isRemote)
    {
        var source = new FakeIdentitySource(sid is null
            ? null
            : new(sid, tokenType, impersonationLevel, isRemote));
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);
        var calls = 0;

        var error = Assert.ThrowsExactly<COMException>(() => guard.Invoke(() => ++calls));

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(1, source.RevertCalls);
    }

    [TestMethod]
    public void OperationFailureStillRevertsAndPreservesOperationError()
    {
        var source = new FakeIdentitySource(new(
            WorkerSid,
            WebAdminBrokerTokenType.Impersonation,
            WebAdminBrokerImpersonationLevel.Impersonation,
            IsRemote: false));
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => guard.Invoke<int>(() => throw new InvalidOperationException("operation failed")));

        Assert.AreEqual("operation failed", error.Message);
        Assert.AreEqual(1, source.RevertCalls);
    }

    [TestMethod]
    public void RevertFailureDeniesEvenAfterAuthorizedOperation()
    {
        var source = new FakeIdentitySource(new(
            WorkerSid,
            WebAdminBrokerTokenType.Impersonation,
            WebAdminBrokerImpersonationLevel.Identification,
            IsRemote: false))
        {
            RevertResult = false
        };
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);
        var calls = 0;

        var error = Assert.ThrowsExactly<COMException>(() => guard.Invoke(() =>
        {
            calls++;
            return "must-deny";
        }));

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(1, source.RevertCalls);
    }

    [TestMethod]
    public void CaptureFailureIsSanitizedAndReverted()
    {
        var source = new FakeIdentitySource(null)
        {
            CaptureException = new InvalidOperationException("native detail")
        };
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);
        var calls = 0;

        var error = Assert.ThrowsExactly<COMException>(() => guard.Invoke(() =>
        {
            calls++;
            return "must-deny";
        }));

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(1, source.RevertCalls);
    }

    [TestMethod]
    public void AuthorizedOperationRunsOnlyAfterRevert()
    {
        var source = new FakeIdentitySource(new(
            WorkerSid,
            WebAdminBrokerTokenType.Impersonation,
            WebAdminBrokerImpersonationLevel.Identification,
            IsRemote: false));
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);

        var result = guard.Invoke(() =>
        {
            Assert.IsTrue(source.HasReverted);
            return "authorized";
        });

        Assert.AreEqual("authorized", result);
        Assert.AreEqual(1, source.RevertCalls);
    }

    [TestMethod]
    public void CallerSuppliedExpectedSidIsNotPartOfGuardInvocation()
    {
        var source = new FakeIdentitySource(new(
            WorkerSid,
            WebAdminBrokerTokenType.Impersonation,
            WebAdminBrokerImpersonationLevel.Identification,
            IsRemote: false));
        var guard = new WebAdminSessionBrokerCallerGuard(WorkerSid, source);

        var method = typeof(WebAdminSessionBrokerCallerGuard).GetMethod(nameof(WebAdminSessionBrokerCallerGuard.Invoke));

        Assert.IsNotNull(method);
        Assert.AreEqual(1, method!.GetParameters().Length);
        Assert.AreEqual(typeof(Func<>), method.GetParameters()[0].ParameterType.GetGenericTypeDefinition());
    }

    private sealed class FakeIdentitySource(WebAdminBrokerCallerIdentity? identity)
        : IWebAdminBrokerCallerIdentitySource
    {
        public int RevertCalls { get; private set; }

        public bool HasReverted { get; private set; }

        public Exception? CaptureException { get; init; }

        public bool RevertResult { get; init; } = true;

        public WebAdminBrokerCallerIdentity? CaptureImpersonatedCaller()
        {
            if (CaptureException is not null)
            {
                throw CaptureException;
            }

            return identity;
        }

        public bool RevertToSelf()
        {
            RevertCalls++;
            HasReverted = true;
            return RevertResult;
        }
    }
}
