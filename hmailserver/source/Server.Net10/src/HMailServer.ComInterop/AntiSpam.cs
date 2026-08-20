using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("998A7E66-21FA-47CC-9DB4-81822F2D05C9")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAntiSpam
{
    [DispId(1)]
    bool GreyListingEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    int GreyListingInitialDelay { get; set; }

    [DispId(3)]
    int GreyListingInitialDelete { get; set; }

    [DispId(4)]
    int GreyListingFinalDelete { get; set; }

    [DispId(6)]
    IInterfaceSURBLServers SURBLServers { get; }

    [DispId(7)]
    bool CheckHostInHelo
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(8)]
    bool AddHeaderSpam
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(9)]
    bool AddHeaderReason
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(10)]
    bool PrependSubject
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(11)]
    string PrependSubjectText
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(12)]
    IInterfaceGreyListingWhiteAddresses GreyListingWhiteAddresses { get; }

    [DispId(13)]
    IInterfaceWhiteListAddresses WhiteListAddresses { get; }

    [DispId(14)]
    int CheckHostInHeloScore { get; set; }

    [DispId(15)]
    int SpamMarkThreshold { get; set; }

    [DispId(16)]
    int SpamDeleteThreshold { get; set; }

    [DispId(17)]
    bool UseSPF
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(18)]
    bool UseMXChecks
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(19)]
    int UseSPFScore { get; set; }

    [DispId(20)]
    int UseMXChecksScore { get; set; }

    [DispId(21)]
    IInterfaceDNSBlackLists DNSBlackLists { get; }

    [DispId(22)]
    int TarpitDelay { get; set; }

    [DispId(23)]
    int TarpitCount { get; set; }

    [DispId(24)]
    bool SpamAssassinEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(25)]
    int SpamAssassinScore { get; set; }

    [DispId(26)]
    bool SpamAssassinMergeScore
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(27)]
    string SpamAssassinHost
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(28)]
    int SpamAssassinPort { get; set; }

    [DispId(29)]
    void ClearGreyListingTriplets();

    [DispId(30)]
    int MaximumMessageSize { get; set; }

    [DispId(31)]
    ComDkimResult DKIMVerify([MarshalAs(UnmanagedType.BStr)] string file);

    [DispId(32)]
    bool DKIMVerificationEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(33)]
    int DKIMVerificationFailureScore { get; set; }

    [DispId(34)]
    bool BypassGreylistingOnSPFSuccess
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(35)]
    bool BypassGreylistingOnMailFromMX
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(36)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestSpamAssassinConnection(
        [MarshalAs(UnmanagedType.BStr)] string hostname,
        int port,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);

    [DispId(37)]
    bool CheckPTR
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(38)]
    int CheckPTRScore { get; set; }
}

[ComVisible(true)]
[Guid("A0B91A99-BCE8-4939-94EC-0881E25A1E5B")]
[ProgId("hMailServer.AntiSpam.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAntiSpam))]
public sealed class AntiSpam : IInterfaceAntiSpam
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private AntiSpamAdministrationSnapshot? _snapshot;
    private readonly IDkimVerificationRuntime? _dkimVerificationRuntime;
    private readonly IGreyListingTripletAdministrationStore? _greyListingTripletStore;
    private readonly ISpamAssassinConnectionTestRuntime? _spamAssassinConnectionTestRuntime;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly ISettingsAdministrationMutationStore? _settingsMutationStore;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;
    private readonly Action<bool>? _publishUseSpf;
    private readonly Action<int>? _publishUseSpfScore;
    private readonly Action<bool>? _publishUseMxChecks;
    private readonly Action<int>? _publishUseMxChecksScore;
    private readonly Action<bool>? _publishSpamAssassinEnabled;
    private readonly Action<int>? _publishSpamAssassinScore;
    private readonly Action<bool>? _publishSpamAssassinMergeScore;
    private readonly Action<string>? _publishSpamAssassinHost;
    private readonly Action<int>? _publishSpamAssassinPort;
    private readonly Action<int>? _publishMaximumMessageSize;
    private readonly Action<bool>? _publishDkimVerificationEnabled;
    private readonly Action<int>? _publishDkimVerificationFailureScore;
    private readonly Action<bool>? _publishBypassGreylistingOnSpfSuccess;
    private readonly Action<bool>? _publishBypassGreylistingOnMailFromMx;

    public AntiSpam()
    {
    }

    private AntiSpam(
        AntiSpamAdministrationSnapshot snapshot,
        IDkimVerificationRuntime? dkimVerificationRuntime,
        IGreyListingTripletAdministrationStore? greyListingTripletStore,
        ISpamAssassinConnectionTestRuntime? spamAssassinConnectionTestRuntime,
        Func<bool>? isServerAdministrator,
        ISettingsAdministrationMutationStore? settingsMutationStore,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory,
        Action<bool>? publishUseSpf,
        Action<int>? publishUseSpfScore,
        Action<bool>? publishUseMxChecks,
        Action<int>? publishUseMxChecksScore,
        Action<bool>? publishSpamAssassinEnabled,
        Action<int>? publishSpamAssassinScore,
        Action<bool>? publishSpamAssassinMergeScore,
        Action<string>? publishSpamAssassinHost,
        Action<int>? publishSpamAssassinPort,
        Action<int>? publishMaximumMessageSize,
        Action<bool>? publishDkimVerificationEnabled,
        Action<int>? publishDkimVerificationFailureScore,
        Action<bool>? publishBypassGreylistingOnSpfSuccess,
        Action<bool>? publishBypassGreylistingOnMailFromMx)
    {
        _snapshot = snapshot;
        _dkimVerificationRuntime = dkimVerificationRuntime;
        _greyListingTripletStore = greyListingTripletStore;
        _spamAssassinConnectionTestRuntime = spamAssassinConnectionTestRuntime;
        _isServerAdministrator = isServerAdministrator;
        _settingsMutationStore = settingsMutationStore;
        _authorizationLeaseFactory = authorizationLeaseFactory;
        _publishUseSpf = publishUseSpf;
        _publishUseSpfScore = publishUseSpfScore;
        _publishUseMxChecks = publishUseMxChecks;
        _publishUseMxChecksScore = publishUseMxChecksScore;
        _publishSpamAssassinEnabled = publishSpamAssassinEnabled;
        _publishSpamAssassinScore = publishSpamAssassinScore;
        _publishSpamAssassinMergeScore = publishSpamAssassinMergeScore;
        _publishSpamAssassinHost = publishSpamAssassinHost;
        _publishSpamAssassinPort = publishSpamAssassinPort;
        _publishMaximumMessageSize = publishMaximumMessageSize;
        _publishDkimVerificationEnabled = publishDkimVerificationEnabled;
        _publishDkimVerificationFailureScore = publishDkimVerificationFailureScore;
        _publishBypassGreylistingOnSpfSuccess = publishBypassGreylistingOnSpfSuccess;
        _publishBypassGreylistingOnMailFromMx = publishBypassGreylistingOnMailFromMx;
    }

    public bool GreyListingEnabled { get => Snapshot.GreyListingEnabled; set => Unavailable(); }

    public int GreyListingInitialDelay { get => Snapshot.GreyListingInitialDelay; set => Unavailable(); }

    public int GreyListingInitialDelete { get => Snapshot.GreyListingInitialDelete; set => Unavailable(); }

    public int GreyListingFinalDelete { get => Snapshot.GreyListingFinalDelete; set => Unavailable(); }

    public IInterfaceSURBLServers SURBLServers
    {
        get
        {
            _ = Snapshot;
            return _isServerAdministrator is not null && !_isServerAdministrator()
                ? new SURBLServers()
                : SurblServerAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public bool CheckHostInHelo { get => Snapshot.CheckHostInHelo; set => Unavailable(); }

    public bool AddHeaderSpam { get => Snapshot.AddHeaderSpam; set => Unavailable(); }

    public bool AddHeaderReason { get => Snapshot.AddHeaderReason; set => Unavailable(); }

    public bool PrependSubject { get => Snapshot.PrependSubject; set => Unavailable(); }

    public string PrependSubjectText { get => Snapshot.PrependSubjectText; set => Unavailable(); }

    public IInterfaceGreyListingWhiteAddresses GreyListingWhiteAddresses
    {
        get
        {
            _ = Snapshot;
            return GreyListingWhiteAddressAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public IInterfaceWhiteListAddresses WhiteListAddresses
    {
        get
        {
            _ = Snapshot;
            return WhiteListAddressAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public int CheckHostInHeloScore { get => Snapshot.CheckHostInHeloScore; set => Unavailable(); }

    public int SpamMarkThreshold { get => Snapshot.SpamMarkThreshold; set => Unavailable(); }

    public int SpamDeleteThreshold { get => Snapshot.SpamDeleteThreshold; set => Unavailable(); }

    public bool UseSPF
    {
        get => Snapshot.UseSpf;
        set => UpdateUseSpf(value);
    }

    public bool UseMXChecks
    {
        get => Snapshot.UseMxChecks;
        set => UpdateUseMxChecks(value);
    }

    public int UseSPFScore
    {
        get => Snapshot.UseSpfScore;
        set => UpdateUseSpfScore(value);
    }

    public int UseMXChecksScore
    {
        get => Snapshot.UseMxChecksScore;
        set => UpdateUseMxChecksScore(value);
    }

    public IInterfaceDNSBlackLists DNSBlackLists
    {
        get
        {
            _ = Snapshot;
            return DnsBlackListAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public int TarpitDelay { get { _ = Snapshot; return 0; } set => IgnoreObsoleteTarpitSetter(); }

    public int TarpitCount { get { _ = Snapshot; return 0; } set => IgnoreObsoleteTarpitSetter(); }

    public bool SpamAssassinEnabled
    {
        get => Snapshot.SpamAssassinEnabled;
        set => UpdateSpamAssassinEnabled(value);
    }

    public int SpamAssassinScore
    {
        get => Snapshot.SpamAssassinScore;
        set => UpdateSpamAssassinScore(value);
    }

    public bool SpamAssassinMergeScore
    {
        get => Snapshot.SpamAssassinMergeScore;
        set => UpdateSpamAssassinMergeScore(value);
    }

    public string SpamAssassinHost
    {
        get => Snapshot.SpamAssassinHost;
        set => UpdateSpamAssassinHost(value);
    }

    public int SpamAssassinPort
    {
        get => Snapshot.SpamAssassinPort;
        set => UpdateSpamAssassinPort(value);
    }

    public void ClearGreyListingTriplets()
    {
        _ = Snapshot;
        if (_greyListingTripletStore is null)
        {
            Unavailable();
            return;
        }

        _greyListingTripletStore
            .ClearAllAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public int MaximumMessageSize
    {
        get => Snapshot.MaximumMessageSize;
        set => UpdateMaximumMessageSize(value);
    }

    public ComDkimResult DKIMVerify(string file)
    {
        _ = Snapshot;
        if (_dkimVerificationRuntime is null)
        {
            return Unavailable<ComDkimResult>();
        }

        return _dkimVerificationRuntime.Verify(file) switch
        {
            DkimVerificationResult.Neutral => ComDkimResult.Neutral,
            DkimVerificationResult.Pass => ComDkimResult.Pass,
            DkimVerificationResult.TempFail => ComDkimResult.TempFail,
            DkimVerificationResult.PermFail => ComDkimResult.PermFail,
            _ => ComDkimResult.TempFail
        };
    }

    public bool DKIMVerificationEnabled
    {
        get => Snapshot.DkimVerificationEnabled;
        set => UpdateDkimVerificationEnabled(value);
    }

    public int DKIMVerificationFailureScore
    {
        get => Snapshot.DkimVerificationFailureScore;
        set => UpdateDkimVerificationFailureScore(value);
    }

    public bool BypassGreylistingOnSPFSuccess
    {
        get => Snapshot.BypassGreylistingOnSpfSuccess;
        set => UpdateBypassGreylistingOnSpfSuccess(value);
    }

    public bool BypassGreylistingOnMailFromMX
    {
        get => Snapshot.BypassGreylistingOnMailFromMx;
        set => UpdateBypassGreylistingOnMailFromMx(value);
    }

    public bool TestSpamAssassinConnection(string hostname, int port, out string resultText)
    {
        resultText = string.Empty;
        _ = Snapshot;
        if (_spamAssassinConnectionTestRuntime is null)
        {
            return Unavailable<bool>();
        }

        if (!LegacyLocalScannerTargetGuard.TryGetValidatedLocalAddress(
                hostname ?? string.Empty,
                out var validatedAddress))
        {
            throw new COMException(
                "Only a locally hosted SpamAssassin scanner can be tested.",
                EFail);
        }

        try
        {
            var result = _spamAssassinConnectionTestRuntime.TestConnection(
                validatedAddress.ToString(),
                port);
            resultText = result.ResultText ?? string.Empty;
            return result.Succeeded;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to test the SpamAssassin connection.",
                EFail);
        }
    }

    public bool CheckPTR { get => Snapshot.CheckPtr; set => Unavailable(); }

    public int CheckPTRScore { get => Snapshot.CheckPtrScore; set => Unavailable(); }

    internal static AntiSpam CreateAuthorized(
        AntiSpamAdministrationSnapshot snapshot,
        IDkimVerificationRuntime? dkimVerificationRuntime = null,
        IGreyListingTripletAdministrationStore? greyListingTripletStore = null,
        ISpamAssassinConnectionTestRuntime? spamAssassinConnectionTestRuntime = null,
        Func<bool>? isServerAdministrator = null,
        ISettingsAdministrationMutationStore? settingsMutationStore = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null,
        Action<bool>? publishUseSpf = null,
        Action<int>? publishUseSpfScore = null,
        Action<bool>? publishUseMxChecks = null,
        Action<int>? publishUseMxChecksScore = null,
        Action<bool>? publishSpamAssassinEnabled = null,
        Action<int>? publishSpamAssassinScore = null,
        Action<bool>? publishSpamAssassinMergeScore = null,
        Action<string>? publishSpamAssassinHost = null,
        Action<int>? publishSpamAssassinPort = null,
        Action<int>? publishMaximumMessageSize = null,
        Action<bool>? publishDkimVerificationEnabled = null,
        Action<int>? publishDkimVerificationFailureScore = null,
        Action<bool>? publishBypassGreylistingOnSpfSuccess = null,
        Action<bool>? publishBypassGreylistingOnMailFromMx = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AntiSpam(
            snapshot,
            dkimVerificationRuntime,
            greyListingTripletStore,
            spamAssassinConnectionTestRuntime,
            isServerAdministrator,
            settingsMutationStore,
            authorizationLeaseFactory,
            publishUseSpf,
            publishUseSpfScore,
            publishUseMxChecks,
            publishUseMxChecksScore,
            publishSpamAssassinEnabled,
            publishSpamAssassinScore,
            publishSpamAssassinMergeScore,
            publishSpamAssassinHost,
            publishSpamAssassinPort,
            publishMaximumMessageSize,
            publishDkimVerificationEnabled,
            publishDkimVerificationFailureScore,
            publishBypassGreylistingOnSpfSuccess,
            publishBypassGreylistingOnMailFromMx);
    }

    internal static AntiSpam CreateDenied() => new();

    private AntiSpamAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "AntiSpam access requires an authenticated server administrator.",
            EAccessDenied);

    private void IgnoreObsoleteTarpitSetter() => _ = Snapshot;

    private void UpdateUseSpf(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamUseSpfAsync(value, CancellationToken.None),
            "The anti-spam SPF setting update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { UseSpf = value };
        }

        _publishUseSpf?.Invoke(value);
    }

    private void UpdateUseSpfScore(int value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamUseSpfScoreAsync(value, CancellationToken.None),
            "The anti-spam SPF score update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { UseSpfScore = value };
        }

        _publishUseSpfScore?.Invoke(value);
    }

    private void UpdateUseMxChecks(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamUseMxChecksAsync(value, CancellationToken.None),
            "The anti-spam MX checks setting update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { UseMxChecks = value };
        }

        _publishUseMxChecks?.Invoke(value);
    }

    private void UpdateUseMxChecksScore(int value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamUseMxChecksScoreAsync(value, CancellationToken.None),
            "The anti-spam MX checks score update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { UseMxChecksScore = value };
        }

        _publishUseMxChecksScore?.Invoke(value);
    }

    private void UpdateSpamAssassinEnabled(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamSpamAssassinEnabledAsync(value, CancellationToken.None),
            "The anti-spam SpamAssassin enabled update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { SpamAssassinEnabled = value };
        }

        _publishSpamAssassinEnabled?.Invoke(value);
    }

    private void UpdateSpamAssassinScore(int value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamSpamAssassinScoreAsync(value, CancellationToken.None),
            "The anti-spam SpamAssassin score update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { SpamAssassinScore = value };
        }

        _publishSpamAssassinScore?.Invoke(value);
    }

    private void UpdateSpamAssassinMergeScore(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamSpamAssassinMergeScoreAsync(value, CancellationToken.None),
            "The anti-spam SpamAssassin merge-score update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { SpamAssassinMergeScore = value };
        }

        _publishSpamAssassinMergeScore?.Invoke(value);
    }

    private void UpdateSpamAssassinHost(string value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamSpamAssassinHostAsync(value ?? string.Empty, CancellationToken.None),
            "The anti-spam SpamAssassin host update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { SpamAssassinHost = value ?? string.Empty };
        }

        _publishSpamAssassinHost?.Invoke(value ?? string.Empty);
    }

    private void UpdateSpamAssassinPort(int value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamSpamAssassinPortAsync(value, CancellationToken.None),
            "The anti-spam SpamAssassin port update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { SpamAssassinPort = value };
        }

        _publishSpamAssassinPort?.Invoke(value);
    }

    private void UpdateMaximumMessageSize(int value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamMaximumMessageSizeAsync(value, CancellationToken.None),
            "The anti-spam maximum message size update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { MaximumMessageSize = value };
        }

        _publishMaximumMessageSize?.Invoke(value);
    }

    private void UpdateDkimVerificationEnabled(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamDkimVerificationEnabledAsync(value, CancellationToken.None),
            "The anti-spam DKIM verification enabled update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { DkimVerificationEnabled = value };
        }

        _publishDkimVerificationEnabled?.Invoke(value);
    }

    private void UpdateDkimVerificationFailureScore(int value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamDkimVerificationFailureScoreAsync(value, CancellationToken.None),
            "The anti-spam DKIM verification failure score update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { DkimVerificationFailureScore = value };
        }

        _publishDkimVerificationFailureScore?.Invoke(value);
    }

    private void UpdateBypassGreylistingOnSpfSuccess(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamBypassGreylistingOnSpfSuccessAsync(value, CancellationToken.None),
            "The anti-spam SPF greylisting bypass update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { BypassGreylistingOnSpfSuccess = value };
        }

        _publishBypassGreylistingOnSpfSuccess?.Invoke(value);
    }

    private void UpdateBypassGreylistingOnMailFromMx(bool value)
    {
        UpdateSetting(
            () => _settingsMutationStore!.UpdateAntiSpamBypassGreylistingOnMailFromMxAsync(value, CancellationToken.None),
            "The anti-spam MailFrom MX greylisting bypass update did not affect the existing settings row.");

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { BypassGreylistingOnMailFromMx = value };
        }

        _publishBypassGreylistingOnMailFromMx?.Invoke(value);
    }

    private void UpdateSetting(
        Func<ValueTask<bool>> update,
        string failureMessage)
    {
        _ = Snapshot;
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Anti-spam settings access requires an authenticated server administrator.",
                EAccessDenied);
        }

        if (_settingsMutationStore is null)
        {
            Unavailable();
            return;
        }

        using var authorizationLease = _authorizationLeaseFactory?
            .Invoke(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (!update().GetAwaiter().GetResult())
        {
            throw new COMException(failureMessage, EFail);
        }
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "AntiSpam mutation, collection, unavailable DKIM verification, greylisting cleanup, and SpamAssassin test methods are not implemented in the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}
