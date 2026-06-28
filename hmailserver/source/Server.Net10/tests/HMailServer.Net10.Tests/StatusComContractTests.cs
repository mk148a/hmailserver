using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class StatusComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsAndCompleteVtableOrder()
    {
        Assert.AreEqual(
            new Guid("C3E2DFFB-BE53-4BE6-BE57-7C5609938CEB"),
            typeof(IInterfaceStatus).GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            typeof(IInterfaceStatus).GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            typeof(IInterfaceStatus).GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        CollectionAssert.AreEqual(
            new[]
            {
                "get_UndeliveredMessages",
                "get_StartTime",
                "get_ProcessedMessages",
                "get_RemovedViruses",
                "get_RemovedSpamMessages",
                "get_SessionCount",
                "get_ThreadID"
            },
            typeof(IInterfaceStatus)
                .GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        Assert.AreEqual(
            1,
            typeof(IInterfaceStatus).GetProperty(nameof(IInterfaceStatus.UndeliveredMessages))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            6,
            typeof(IInterfaceStatus).GetMethod(nameof(IInterfaceStatus.get_SessionCount))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            typeof(IInterfaceStatus).GetProperty(nameof(IInterfaceStatus.ThreadID))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Status);

        Assert.AreEqual(new Guid("ADD8B04F-F7A0-4C73-8B0B-E53B3077F052"), type.GUID);
        Assert.AreEqual("hMailServer.Status.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceStatus), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var statusError = Assert.ThrowsExactly<COMException>(() => _ = new Status().ProcessedMessages);

        Assert.AreEqual(EAccessDenied, statusError.ErrorCode);
    }

    [TestMethod]
    public void ApplicationStatus_RequiresServerAdministratorAndUsesConfiguredRuntimeSnapshot()
    {
        StatusAdministrationRuntimeHost.Configure(
            new FixedServerStatusAdministrationStore(
                new ServerStatusSnapshot(
                    UndeliveredMessages: "42\t2026-06-28 10:20:30\tsender@example.test\trecipient@example.test\t1901-01-01 00:00:00\tC:\\hMailServer\\Data\\42.eml\t0\t2",
                    StartTime: "2026-06-28 01:02:03",
                    ProcessedMessages: 11,
                    RemovedViruses: 3,
                    RemovedSpamMessages: 5,
                    SessionCounts: new Dictionary<int, int>
                    {
                        [(int)ComSessionType.Smtp] = 7,
                        [(int)ComSessionType.Pop3] = 2,
                        [(int)ComSessionType.Imap] = 4
                    },
                    ThreadID: 1234)));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Status);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        var status = application.Status;

        Assert.AreEqual(
            "42\t2026-06-28 10:20:30\tsender@example.test\trecipient@example.test\t1901-01-01 00:00:00\tC:\\hMailServer\\Data\\42.eml\t0\t2",
            status.UndeliveredMessages);
        Assert.AreEqual("2026-06-28 01:02:03", status.StartTime);
        Assert.AreEqual(11, status.ProcessedMessages);
        Assert.AreEqual(3, status.RemovedViruses);
        Assert.AreEqual(5, status.RemovedSpamMessages);
        Assert.AreEqual(7, status.get_SessionCount(ComSessionType.Smtp));
        Assert.AreEqual(2, status.get_SessionCount(ComSessionType.Pop3));
        Assert.AreEqual(4, status.get_SessionCount(ComSessionType.Imap));
        Assert.AreEqual(0, status.get_SessionCount(ComSessionType.Unknown));
        Assert.AreEqual(1234, status.ThreadID);
    }

    private sealed class RecordingAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class FixedServerStatusAdministrationStore(ServerStatusSnapshot snapshot)
        : IServerStatusAdministrationStore
    {
        public ValueTask<ServerStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(snapshot);
    }
}
