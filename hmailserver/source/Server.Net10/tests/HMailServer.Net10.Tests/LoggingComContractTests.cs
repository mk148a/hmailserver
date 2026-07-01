using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LoggingComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsAndCompleteVtableOrder()
    {
        var contract = typeof(IInterfaceLogging);

        Assert.AreEqual(new Guid("AAD8A0DF-2963-4C5B-A906-6B07B9CC0643"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        CollectionAssert.AreEqual(
            new[]
            {
                "get_Enabled",
                "set_Enabled",
                "get_LogSMTP",
                "set_LogSMTP",
                "get_LogPOP3",
                "set_LogPOP3",
                "get_LogTCPIP",
                "set_LogTCPIP",
                "get_LogApplication",
                "set_LogApplication",
                "get_Device",
                "set_Device",
                "get_LogFormat",
                "set_LogFormat",
                "get_LogDebug",
                "set_LogDebug",
                "get_LogIMAP",
                "set_LogIMAP",
                "EnableLiveLogging",
                "get_Directory",
                "get_LiveLog",
                "get_AWStatsEnabled",
                "set_AWStatsEnabled",
                "get_MaskPasswordsInLog",
                "set_MaskPasswordsInLog",
                "get_CurrentEventLog",
                "get_CurrentErrorLog",
                "get_CurrentAwstatsLog",
                "get_CurrentDefaultLog",
                "get_KeepFilesOpen",
                "set_KeepFilesOpen",
                "get_LiveLoggingEnabled"
            },
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        AssertMember(contract, nameof(IInterfaceLogging.Enabled), 1, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogSMTP), 2, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogPOP3), 3, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogTCPIP), 4, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogApplication), 5, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.Device), 9, typeof(ComLogDevice), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogFormat), 10, typeof(ComLogOutputFormat), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogDebug), 11, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LogIMAP), 12, typeof(bool), canWrite: true);
        AssertMethod(contract, nameof(IInterfaceLogging.EnableLiveLogging), 13);
        AssertMember(contract, nameof(IInterfaceLogging.Directory), 14, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceLogging.LiveLog), 15, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceLogging.AWStatsEnabled), 16, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.MaskPasswordsInLog), 17, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.CurrentEventLog), 18, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceLogging.CurrentErrorLog), 19, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceLogging.CurrentAwstatsLog), 20, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceLogging.CurrentDefaultLog), 21, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceLogging.KeepFilesOpen), 22, typeof(bool), canWrite: true);
        AssertMember(contract, nameof(IInterfaceLogging.LiveLoggingEnabled), 23, typeof(bool), canWrite: false);
    }

    [TestMethod]
    public void BooleanProperties_PreserveVariantBoolMarshaling()
    {
        var names = new[]
        {
            nameof(IInterfaceLogging.Enabled),
            nameof(IInterfaceLogging.LogSMTP),
            nameof(IInterfaceLogging.LogPOP3),
            nameof(IInterfaceLogging.LogTCPIP),
            nameof(IInterfaceLogging.LogApplication),
            nameof(IInterfaceLogging.LogDebug),
            nameof(IInterfaceLogging.LogIMAP),
            nameof(IInterfaceLogging.AWStatsEnabled),
            nameof(IInterfaceLogging.MaskPasswordsInLog),
            nameof(IInterfaceLogging.KeepFilesOpen),
            nameof(IInterfaceLogging.LiveLoggingEnabled)
        };

        foreach (var name in names)
        {
            var property = typeof(IInterfaceLogging).GetProperty(name);

            Assert.IsNotNull(property);
            Assert.AreEqual(
                UnmanagedType.VariantBool,
                property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
            if (property.SetMethod is not null)
            {
                Assert.AreEqual(
                    UnmanagedType.VariantBool,
                    property.SetMethod.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
            }
        }

        var parameter = typeof(IInterfaceLogging)
            .GetMethod(nameof(IInterfaceLogging.EnableLiveLogging))
            ?.GetParameters()
            .Single();
        Assert.AreEqual(UnmanagedType.VariantBool, parameter?.GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void StringProperties_PreserveBstrMarshaling()
    {
        var names = new[]
        {
            nameof(IInterfaceLogging.Directory),
            nameof(IInterfaceLogging.LiveLog),
            nameof(IInterfaceLogging.CurrentEventLog),
            nameof(IInterfaceLogging.CurrentErrorLog),
            nameof(IInterfaceLogging.CurrentAwstatsLog),
            nameof(IInterfaceLogging.CurrentDefaultLog)
        };

        foreach (var name in names)
        {
            var property = typeof(IInterfaceLogging).GetProperty(name);

            Assert.IsNotNull(property);
            Assert.AreEqual(
                UnmanagedType.BStr,
                property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    [TestMethod]
    public void Enums_PreserveLegacyGuidsAndValues()
    {
        Assert.AreEqual(new Guid("027282DE-4C3A-11D9-93CE-D4EDF9405FEE"), typeof(ComLogDevice).GUID);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2 },
            Enum.GetValues<ComLogDevice>().Select(static value => Convert.ToInt32(value)).ToArray());

        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD00"), typeof(ComLogOutputFormat).GUID);
        CollectionAssert.AreEqual(
            new[] { 1, 2 },
            Enum.GetValues<ComLogOutputFormat>().Select(static value => Convert.ToInt32(value)).ToArray());
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Logging);

        Assert.AreEqual(new Guid("E3E22438-871F-49CF-A47E-4D3A144BD002"), type.GUID);
        Assert.AreEqual("hMailServer.Logging.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceLogging), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var loggingError = Assert.ThrowsExactly<COMException>(() => _ = new Logging().Enabled);
        var directoryError = Assert.ThrowsExactly<COMException>(() => _ = new Logging().Directory);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Logging);

        Assert.AreEqual(EAccessDenied, loggingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, directoryError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedLogging_ExposesReadOnlySnapshotAndKeepsMutationsPending()
    {
        IInterfaceLogging logging = Logging.CreateAuthorized(
            new LoggingAdministrationSnapshot(
                LoggingMask: 1 + 2 + 8 + 16 + 32 + 64 + 256,
                Device: 2,
                LogFormat: 1,
                AwStatsEnabled: true,
                Directory: @"C:\hMailServer\Logs\"));

        Assert.IsTrue(logging.Enabled);
        Assert.IsTrue(logging.LogSMTP);
        Assert.IsFalse(logging.LogPOP3);
        Assert.IsTrue(logging.LogTCPIP);
        Assert.IsTrue(logging.LogApplication);
        Assert.AreEqual(ComLogDevice.File, logging.Device);
        Assert.AreEqual(ComLogOutputFormat.Csa, logging.LogFormat);
        Assert.IsTrue(logging.LogDebug);
        Assert.IsTrue(logging.LogIMAP);
        Assert.AreEqual(@"C:\hMailServer\Logs\", logging.Directory);
        Assert.IsTrue(logging.AWStatsEnabled);
        Assert.IsTrue(logging.KeepFilesOpen);

        AssertPending(() => logging.Enabled = false);
        AssertPending(() => logging.LogSMTP = false);
        AssertPending(() => logging.LogPOP3 = true);
        AssertPending(() => logging.LogTCPIP = false);
        AssertPending(() => logging.LogApplication = false);
        AssertPending(() => logging.Device = ComLogDevice.Unknown);
        AssertPending(() => logging.LogFormat = ComLogOutputFormat.Default);
        AssertPending(() => logging.LogDebug = false);
        AssertPending(() => logging.LogIMAP = false);
        AssertPending(() => logging.EnableLiveLogging(true));
        AssertPending(() => _ = logging.LiveLog);
        AssertPending(() => logging.AWStatsEnabled = false);
        AssertPending(() => _ = logging.MaskPasswordsInLog);
        AssertPending(() => logging.MaskPasswordsInLog = false);
        AssertPending(() => _ = logging.CurrentEventLog);
        AssertPending(() => _ = logging.CurrentErrorLog);
        AssertPending(() => _ = logging.CurrentAwstatsLog);
        AssertPending(() => _ = logging.CurrentDefaultLog);
        AssertPending(() => logging.KeepFilesOpen = false);
        AssertPending(() => _ = logging.LiveLoggingEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesConfiguredLoggingSnapshot()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                LoggingMask: 1 + 4 + 8,
                LogDevice: 1,
                LogFormat: 0,
                AwStatsEnabled: false),
            new SettingsRuntimeConfiguration(
                LoggingDirectory: @"E:\hMailServer\Logs\"));

        var logging = settings.Logging;

        Assert.IsTrue(logging.Enabled);
        Assert.IsFalse(logging.LogSMTP);
        Assert.IsTrue(logging.LogPOP3);
        Assert.IsTrue(logging.LogTCPIP);
        Assert.IsFalse(logging.LogApplication);
        Assert.AreEqual(ComLogDevice.Sql, logging.Device);
        Assert.AreEqual(ComLogOutputFormat.Default, logging.LogFormat);
        Assert.IsFalse(logging.LogDebug);
        Assert.IsFalse(logging.LogIMAP);
        Assert.AreEqual(@"E:\hMailServer\Logs\", logging.Directory);
        Assert.IsFalse(logging.AWStatsEnabled);
        Assert.IsFalse(logging.KeepFilesOpen);
    }

    private static void AssertMember(
        Type contract,
        string name,
        int dispatchId,
        Type propertyType,
        bool canWrite)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(propertyType, property.PropertyType);
        Assert.AreEqual(canWrite, property.CanWrite);
    }

    private static void AssertMethod(Type contract, string name, int dispatchId)
    {
        var method = contract.GetMethod(name);

        Assert.IsNotNull(method);
        Assert.AreEqual(dispatchId, method.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(void), method.ReturnType);
        Assert.AreEqual(1, method.GetParameters().Length);
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }
}
