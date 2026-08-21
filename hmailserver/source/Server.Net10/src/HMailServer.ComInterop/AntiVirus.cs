using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("952EE84F-C1D4-4869-8B86-76A3BA8F39FA")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAntiVirus
{
    [DispId(1)]
    bool ClamWinEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    string ClamWinExecutable
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    string ClamWinDBFolder
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(4)]
    ComAntivirusAction Action { get; set; }

    [DispId(5)]
    bool NotifyReceiver
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(6)]
    bool NotifySender
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(7)]
    bool CustomScannerEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(8)]
    string CustomScannerExecutable
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(9)]
    int CustomScannerReturnValue { get; set; }

    [DispId(10)]
    int MaximumMessageSize { get; set; }

    [DispId(11)]
    IInterfaceBlockedAttachments BlockedAttachments { get; }

    [DispId(12)]
    bool EnableAttachmentBlocking
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(13)]
    bool ClamAVEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(14)]
    string ClamAVHost
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(15)]
    int ClamAVPort { get; set; }

    [DispId(16)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestCustomerScanner(
        [MarshalAs(UnmanagedType.BStr)] string customExecutable,
        int virusReturnCode,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);

    [DispId(17)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestClamWinScanner(
        [MarshalAs(UnmanagedType.BStr)] string clamWinExecutable,
        [MarshalAs(UnmanagedType.BStr)] string clamWinDatabase,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);

    [DispId(18)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestClamAVScanner(
        [MarshalAs(UnmanagedType.BStr)] string clamAVHostName,
        int clamAVPort,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);
}

[ComVisible(true)]
[Guid("82D6DBF9-DDDB-4C4A-A52A-92B6ED16D8EA")]
[ProgId("hMailServer.AntiVirus.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAntiVirus))]
public sealed class AntiVirus : IInterfaceAntiVirus
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private AntiVirusAdministrationSnapshot? _snapshot;
    private readonly IClamAvScannerTestRuntime? _clamAvScannerTestRuntime;
    private readonly IClamWinScannerTestRuntime? _clamWinScannerTestRuntime;
    private readonly ICustomScannerTestRuntime? _customScannerTestRuntime;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly ISettingsAdministrationMutationStore? _settingsMutationStore;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;
    private readonly Action<bool>? _publishClamWinEnabled;
    private readonly Action<string>? _publishClamWinExecutable;
    private readonly Action<string>? _publishClamWinDatabase;
    private readonly Action<int>? _publishAction;
    private readonly Action<bool>? _publishNotifyReceiver;
    private readonly Action<bool>? _publishNotifySender;
    private readonly Action<bool>? _publishCustomScannerEnabled;

    public AntiVirus()
    {
    }

    private AntiVirus(
        AntiVirusAdministrationSnapshot snapshot,
        IClamAvScannerTestRuntime? clamAvScannerTestRuntime,
        IClamWinScannerTestRuntime? clamWinScannerTestRuntime,
        ICustomScannerTestRuntime? customScannerTestRuntime,
        Func<bool>? isServerAdministrator,
        ISettingsAdministrationMutationStore? settingsMutationStore,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory,
        Action<bool>? publishClamWinEnabled,
        Action<string>? publishClamWinExecutable,
        Action<string>? publishClamWinDatabase,
        Action<int>? publishAction,
        Action<bool>? publishNotifyReceiver,
        Action<bool>? publishNotifySender,
        Action<bool>? publishCustomScannerEnabled)
    {
        _snapshot = snapshot;
        _clamAvScannerTestRuntime = clamAvScannerTestRuntime;
        _clamWinScannerTestRuntime = clamWinScannerTestRuntime;
        _customScannerTestRuntime = customScannerTestRuntime;
        _isServerAdministrator = isServerAdministrator;
        _settingsMutationStore = settingsMutationStore;
        _authorizationLeaseFactory = authorizationLeaseFactory;
        _publishClamWinEnabled = publishClamWinEnabled;
        _publishClamWinExecutable = publishClamWinExecutable;
        _publishClamWinDatabase = publishClamWinDatabase;
        _publishAction = publishAction;
        _publishNotifyReceiver = publishNotifyReceiver;
        _publishNotifySender = publishNotifySender;
        _publishCustomScannerEnabled = publishCustomScannerEnabled;
    }

    public bool ClamWinEnabled
    {
        get => Snapshot.ClamWinEnabled;
        set
        {
            _ = Snapshot;
            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusClamWinEnabledAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The ClamWin enabled update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { ClamWinEnabled = value };
            }

            _publishClamWinEnabled?.Invoke(value);
        }
    }

    public string ClamWinExecutable
    {
        get => Snapshot.ClamWinExecutable;
        set
        {
            _ = Snapshot;
            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusClamWinExecutableAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The ClamWin executable update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { ClamWinExecutable = value };
            }

            _publishClamWinExecutable?.Invoke(value);
        }
    }

    public string ClamWinDBFolder
    {
        get => Snapshot.ClamWinDatabase;
        set
        {
            _ = Snapshot;
            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusClamWinDatabaseAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The ClamWin database update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { ClamWinDatabase = value };
            }

            _publishClamWinDatabase?.Invoke(value);
        }
    }

    public ComAntivirusAction Action
    {
        get => Snapshot.Action == (int)ComAntivirusAction.DeleteAttachments
            ? ComAntivirusAction.DeleteAttachments
            : ComAntivirusAction.DeleteEmail;
        set
        {
            _ = Snapshot;
            var action = value switch
            {
                ComAntivirusAction.DeleteEmail => (int)ComAntivirusAction.DeleteEmail,
                ComAntivirusAction.DeleteAttachments => (int)ComAntivirusAction.DeleteAttachments,
                _ => throw new COMException("The anti-virus action is not supported.", EFail)
            };

            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusActionAsync(action, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The anti-virus action update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { Action = action };
            }

            _publishAction?.Invoke(action);
        }
    }

    public bool NotifyReceiver
    {
        get => Snapshot.NotifyReceiver;
        set
        {
            _ = Snapshot;
            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusNotifyReceiverAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The anti-virus receiver notification update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { NotifyReceiver = value };
            }

            _publishNotifyReceiver?.Invoke(value);
        }
    }

    public bool NotifySender
    {
        get => Snapshot.NotifySender;
        set
        {
            _ = Snapshot;
            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusNotifySenderAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The anti-virus sender notification update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { NotifySender = value };
            }

            _publishNotifySender?.Invoke(value);
        }
    }

    public bool CustomScannerEnabled
    {
        get => Snapshot.CustomScannerEnabled;
        set
        {
            _ = Snapshot;
            if (_settingsMutationStore is null)
            {
                Unavailable();
                return;
            }

            using var authorizationLease = _authorizationLeaseFactory is null
                ? null
                : _authorizationLeaseFactory(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    ?? throw new COMException(
                        "Anti-virus settings access requires an authenticated server administrator.",
                        EAccessDenied);

            if (!_settingsMutationStore
                .UpdateAntiVirusCustomScannerEnabledAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The custom scanner enabled update did not affect the existing settings row.",
                    EFail);
            }

            if (_snapshot is not null)
            {
                _snapshot = _snapshot with { CustomScannerEnabled = value };
            }

            _publishCustomScannerEnabled?.Invoke(value);
        }
    }

    public string CustomScannerExecutable { get => Snapshot.CustomScannerExecutable; set => Unavailable(); }

    public int CustomScannerReturnValue { get => Snapshot.CustomScannerReturnValue; set => Unavailable(); }

    public int MaximumMessageSize { get => Snapshot.MaximumMessageSize; set => Unavailable(); }

    public IInterfaceBlockedAttachments BlockedAttachments
    {
        get
        {
            _ = Snapshot;
            return BlockedAttachmentAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public bool EnableAttachmentBlocking { get => Snapshot.EnableAttachmentBlocking; set => Unavailable(); }

    public bool ClamAVEnabled { get => Snapshot.ClamAvEnabled; set => Unavailable(); }

    public string ClamAVHost { get => Snapshot.ClamAvHost; set => Unavailable(); }

    public int ClamAVPort { get => Snapshot.ClamAvPort; set => Unavailable(); }

    public bool TestCustomerScanner(string customExecutable, int virusReturnCode, out string resultText)
    {
        resultText = string.Empty;
        _ = Snapshot;
        if (_customScannerTestRuntime is null)
        {
            return Unavailable<bool>();
        }

        try
        {
            var result = _customScannerTestRuntime.TestConnection(
                customExecutable ?? string.Empty,
                virusReturnCode);
            resultText = result.ResultText ?? string.Empty;
            return result.Succeeded;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to test the custom virus scanner.",
                EFail);
        }
    }

    public bool TestClamWinScanner(string clamWinExecutable, string clamWinDatabase, out string resultText)
    {
        resultText = string.Empty;
        _ = Snapshot;
        if (_clamWinScannerTestRuntime is null)
        {
            return Unavailable<bool>();
        }

        try
        {
            var result = _clamWinScannerTestRuntime.TestConnection(
                clamWinExecutable ?? string.Empty,
                clamWinDatabase ?? string.Empty);
            resultText = result.ResultText ?? string.Empty;
            return result.Succeeded;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to test the ClamWin scanner.",
                EFail);
        }
    }

    public bool TestClamAVScanner(string clamAVHostName, int clamAVPort, out string resultText)
    {
        resultText = string.Empty;
        _ = Snapshot;
        if (_clamAvScannerTestRuntime is null)
        {
            return Unavailable<bool>();
        }

        if (!LegacyLocalScannerTargetGuard.TryGetValidatedLocalAddress(
                clamAVHostName ?? string.Empty,
                out var validatedAddress))
        {
            throw new COMException(
                "It was not possible to test the ClamAV connection.",
                EFail);
        }

        try
        {
            var result = _clamAvScannerTestRuntime.TestConnection(
                validatedAddress.ToString(),
                clamAVPort);
            resultText = result.ResultText ?? string.Empty;
            return result.Succeeded;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to test the ClamAV connection.",
                EFail);
        }
    }

    internal static AntiVirus CreateAuthorized(
        AntiVirusAdministrationSnapshot snapshot,
        IClamAvScannerTestRuntime? clamAvScannerTestRuntime = null,
        IClamWinScannerTestRuntime? clamWinScannerTestRuntime = null,
        ICustomScannerTestRuntime? customScannerTestRuntime = null,
        Func<bool>? isServerAdministrator = null,
        ISettingsAdministrationMutationStore? settingsMutationStore = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null,
        Action<bool>? publishClamWinEnabled = null,
        Action<string>? publishClamWinExecutable = null,
        Action<string>? publishClamWinDatabase = null,
        Action<int>? publishAction = null,
        Action<bool>? publishNotifyReceiver = null,
        Action<bool>? publishNotifySender = null,
        Action<bool>? publishCustomScannerEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AntiVirus(
            snapshot,
            clamAvScannerTestRuntime,
            clamWinScannerTestRuntime,
            customScannerTestRuntime,
            isServerAdministrator,
            settingsMutationStore,
            authorizationLeaseFactory,
            publishClamWinEnabled,
            publishClamWinExecutable,
            publishClamWinDatabase,
            publishAction,
            publishNotifyReceiver,
            publishNotifySender,
            publishCustomScannerEnabled);
    }

    private AntiVirusAdministrationSnapshot Snapshot
    {
        get
        {
            if (_isServerAdministrator is not null && !_isServerAdministrator())
            {
                throw new COMException(
                    "AntiVirus access requires an authenticated server administrator.",
                    EAccessDenied);
            }

            return _snapshot ?? throw new COMException(
                "AntiVirus access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "AntiVirus mutation and scanner test methods are not implemented in the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}
