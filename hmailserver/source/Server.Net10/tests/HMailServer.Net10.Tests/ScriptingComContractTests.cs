using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using ScriptingComClass = HMailServer.ComInterop.Scripting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ScriptingComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndMarshaling()
    {
        var contract = typeof(IInterfaceScripting);

        Assert.AreEqual(new Guid("B1EA04C0-B0B7-4638-80E4-41278CEF8C19"), contract.GUID);
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
                "get_Language",
                "set_Language",
                "Reload",
                "CheckSyntax",
                "get_Directory",
                "get_CurrentScriptFile"
            },
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        var enabled = contract.GetProperty(nameof(IInterfaceScripting.Enabled));
        Assert.IsNotNull(enabled);
        Assert.AreEqual(1, enabled.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, enabled.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, enabled.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);

        AssertBstrProperty(contract, nameof(IInterfaceScripting.Language), 2, canWrite: true);
        Assert.AreEqual(3, contract.GetMethod(nameof(IInterfaceScripting.Reload))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var checkSyntax = contract.GetMethod(nameof(IInterfaceScripting.CheckSyntax));
        Assert.AreEqual(4, checkSyntax?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, checkSyntax?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        AssertBstrProperty(contract, nameof(IInterfaceScripting.Directory), 5, canWrite: false);
        AssertBstrProperty(contract, nameof(IInterfaceScripting.CurrentScriptFile), 6, canWrite: false);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(ScriptingComClass);

        Assert.AreEqual(new Guid("68A73A47-5B56-43A3-BC11-CFC436F3BA9E"), type.GUID);
        Assert.AreEqual("hMailServer.Scripting.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceScripting), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var scriptingError = Assert.ThrowsExactly<COMException>(() => _ = new ScriptingComClass().Enabled);
        var currentScriptFileError = Assert.ThrowsExactly<COMException>(() => _ = new ScriptingComClass().CurrentScriptFile);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Scripting);

        Assert.AreEqual(EAccessDenied, scriptingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, currentScriptFileError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedScripting_ExposesReadOnlySnapshotAndKeepsOperationsPending()
    {
        IInterfaceScripting scripting = ScriptingComClass.CreateAuthorized(
            new ScriptingAdministrationSnapshot(
                Enabled: true,
                Language: "JScript",
                Directory: @"C:\hMailServer\Events\"));

        Assert.IsTrue(scripting.Enabled);
        Assert.AreEqual("JScript", scripting.Language);
        Assert.AreEqual(@"C:\hMailServer\Events\", scripting.Directory);
        Assert.AreEqual(@"C:\hMailServer\Events\\EventHandlers.js", scripting.CurrentScriptFile);

        AssertPending(() => scripting.Enabled = false);
        AssertPending(() => scripting.Language = "VBScript");
        AssertPending(scripting.Reload);
        AssertPending(() => _ = scripting.CheckSyntax());
    }

    [TestMethod]
    public void AuthorizedScripting_CurrentScriptFileUsesLegacyCaseSensitiveExtensionMappingWithoutFileAccess()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hmailserver-events-{Guid.NewGuid():N}") + "\\";
        var cases = new[]
        {
            ("VBScript", "vbs"),
            ("JScript", "js"),
            ("vbscript", string.Empty),
            ("Unknown", string.Empty)
        };

        Assert.IsFalse(System.IO.Directory.Exists(directory));

        foreach (var (language, extension) in cases)
        {
            IInterfaceScripting scripting = ScriptingComClass.CreateAuthorized(
                new ScriptingAdministrationSnapshot(
                    Enabled: true,
                    Language: language,
                    Directory: directory));

            Assert.AreEqual($"{directory}\\EventHandlers.{extension}", scripting.CurrentScriptFile, language);
        }

        Assert.IsFalse(System.IO.Directory.Exists(directory));
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesConfiguredScriptingSnapshot()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                UseScriptServer: true,
                ScriptLanguage: "VBScript"),
            new SettingsRuntimeConfiguration(
                ScriptingDirectory: @"E:\hMailServer\Events\"));

        var scripting = settings.Scripting;

        Assert.IsTrue(scripting.Enabled);
        Assert.AreEqual("VBScript", scripting.Language);
        Assert.AreEqual(@"E:\hMailServer\Events\", scripting.Directory);
        Assert.AreEqual(@"E:\hMailServer\Events\\EventHandlers.vbs", scripting.CurrentScriptFile);
    }

    private static void AssertBstrProperty(Type contract, string name, int dispatchId, bool canWrite)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(canWrite, property.CanWrite);
        if (canWrite)
        {
            Assert.AreEqual(UnmanagedType.BStr, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }
}
