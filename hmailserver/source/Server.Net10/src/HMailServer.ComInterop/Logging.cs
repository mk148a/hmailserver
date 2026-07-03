using System.Globalization;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("AAD8A0DF-2963-4C5B-A906-6B07B9CC0643")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceLogging
{
    [DispId(1)]
    bool Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    bool LogSMTP
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(3)]
    bool LogPOP3
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(4)]
    bool LogTCPIP
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(5)]
    bool LogApplication
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(9)]
    ComLogDevice Device { get; set; }

    [DispId(10)]
    ComLogOutputFormat LogFormat { get; set; }

    [DispId(11)]
    bool LogDebug
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(12)]
    bool LogIMAP
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(13)]
    void EnableLiveLogging([MarshalAs(UnmanagedType.VariantBool)] bool newVal);

    [DispId(14)]
    string Directory { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(15)]
    string LiveLog { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(16)]
    bool AWStatsEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(17)]
    bool MaskPasswordsInLog
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(18)]
    string CurrentEventLog { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(19)]
    string CurrentErrorLog { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(20)]
    string CurrentAwstatsLog { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(21)]
    string CurrentDefaultLog { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(22)]
    bool KeepFilesOpen
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(23)]
    bool LiveLoggingEnabled { [return: MarshalAs(UnmanagedType.VariantBool)] get; }
}

[ComVisible(true)]
[Guid("E3E22438-871F-49CF-A47E-4D3A144BD002")]
[ProgId("hMailServer.Logging.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceLogging))]
public sealed class Logging : LoggingComAdapter
{
    private const int EnabledFlag = 1;
    private const int SmtpFlag = 2;
    private const int Pop3Flag = 4;
    private const int TcpIpFlag = 8;
    private const int ApplicationFlag = 16;
    private const int DebugFlag = 32;
    private const int ImapFlag = 64;
    private const int KeepFilesOpenFlag = 256;
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly LoggingAdministrationSnapshot? _snapshot;
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly ILoggingLiveLogRuntime _liveLogRuntime = LoggingLiveLogRuntimeHost.Current;

    public Logging()
    {
    }

    private Logging(
        LoggingAdministrationSnapshot snapshot,
        TimeProvider? timeProvider,
        ILoggingLiveLogRuntime? liveLogRuntime)
    {
        _snapshot = snapshot;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _liveLogRuntime = liveLogRuntime ?? LoggingLiveLogRuntimeHost.Current;
    }

    public override bool Enabled { get => HasFlag(EnabledFlag); set => base.Enabled = value; }

    public override bool LogSMTP { get => HasFlag(SmtpFlag); set => base.LogSMTP = value; }

    public override bool LogPOP3 { get => HasFlag(Pop3Flag); set => base.LogPOP3 = value; }

    public override bool LogTCPIP { get => HasFlag(TcpIpFlag); set => base.LogTCPIP = value; }

    public override bool LogApplication { get => HasFlag(ApplicationFlag); set => base.LogApplication = value; }

    public override ComLogDevice Device
    {
        get => Snapshot.Device switch
        {
            1 => ComLogDevice.Sql,
            2 => ComLogDevice.File,
            _ => ComLogDevice.Unknown
        };
        set => base.Device = value;
    }

    public override ComLogOutputFormat LogFormat
    {
        get => Snapshot.LogFormat == 1
            ? ComLogOutputFormat.Csa
            : ComLogOutputFormat.Default;
        set => base.LogFormat = value;
    }

    public override bool LogDebug { get => HasFlag(DebugFlag); set => base.LogDebug = value; }

    public override bool LogIMAP { get => HasFlag(ImapFlag); set => base.LogIMAP = value; }

    public override void EnableLiveLogging(bool newVal)
    {
        _ = Snapshot;
        _liveLogRuntime.Enable(newVal);
    }

    public override string Directory => Snapshot.Directory;

    public override string LiveLog
    {
        get
        {
            _ = Snapshot;
            return _liveLogRuntime.ReadAndClear();
        }
    }

    public override bool AWStatsEnabled { get => Snapshot.AwStatsEnabled; set => base.AWStatsEnabled = value; }

    public override bool MaskPasswordsInLog
    {
        get
        {
            _ = Snapshot;
            return false;
        }
        set => _ = Snapshot;
    }

    public override string CurrentEventLog => BuildCurrentLogPath("hmailserver_events.log");

    public override string CurrentErrorLog =>
        BuildCurrentLogPath($"ERROR_hmailserver_{GetCurrentDate()}.log");

    public override string CurrentAwstatsLog => BuildCurrentLogPath("hmailserver_awstats.log");

    public override string CurrentDefaultLog =>
        BuildCurrentLogPath($"hmailserver_{GetCurrentDate()}.log");

    public override bool KeepFilesOpen { get => HasFlag(KeepFilesOpenFlag); set => base.KeepFilesOpen = value; }

    public override bool LiveLoggingEnabled
    {
        get
        {
            _ = Snapshot;
            return _liveLogRuntime.Enabled;
        }
    }

    internal static Logging CreateAuthorized(
        LoggingAdministrationSnapshot snapshot,
        TimeProvider? timeProvider = null,
        ILoggingLiveLogRuntime? liveLogRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Logging(snapshot, timeProvider, liveLogRuntime);
    }

    private LoggingAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Logging access requires an authenticated server administrator.",
            EAccessDenied);

    private bool HasFlag(int flag) => (Snapshot.LoggingMask & flag) != 0;

    private string BuildCurrentLogPath(string fileName) => $"{Snapshot.Directory}\\{fileName}";

    private string GetCurrentDate() =>
        _timeProvider.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

[ComVisible(false)]
public abstract class LoggingComAdapter : IInterfaceLogging
{
    public virtual bool Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool LogSMTP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool LogPOP3 { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool LogTCPIP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool LogApplication { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual ComLogDevice Device { get => Unavailable<ComLogDevice>(); set => Unavailable(); }
    public virtual ComLogOutputFormat LogFormat { get => Unavailable<ComLogOutputFormat>(); set => Unavailable(); }
    public virtual bool LogDebug { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool LogIMAP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual void EnableLiveLogging(bool newVal) => Unavailable();
    public virtual string Directory => Unavailable<string>();
    public virtual string LiveLog => Unavailable<string>();
    public virtual bool AWStatsEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool MaskPasswordsInLog { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string CurrentEventLog => Unavailable<string>();
    public virtual string CurrentErrorLog => Unavailable<string>();
    public virtual string CurrentAwstatsLog => Unavailable<string>();
    public virtual string CurrentDefaultLog => Unavailable<string>();
    public virtual bool KeepFilesOpen { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool LiveLoggingEnabled => Unavailable<bool>();

    private T Unavailable<T>() => LoggingComAuthorization.Unavailable<T>(this);

    private void Unavailable() => LoggingComAuthorization.Unavailable(this);
}

[ComVisible(false)]
internal static class LoggingComAuthorization
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    internal static T Unavailable<T>(IInterfaceLogging logging)
    {
        EnsureAuthorized(logging);
        throw new COMException(
            "This Logging member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    internal static void Unavailable(IInterfaceLogging logging)
    {
        EnsureAuthorized(logging);
        throw new COMException(
            "This Logging member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private static void EnsureAuthorized(IInterfaceLogging logging)
    {
        if (logging is Logging authorized)
        {
            _ = authorized.Enabled;
            return;
        }

        throw new COMException(
            "Logging access requires an authenticated server administrator.",
            EAccessDenied);
    }
}
