namespace HMailServer.Core.Abstractions;

public sealed record AntiVirusAdministrationSnapshot(
    bool ClamWinEnabled,
    string ClamWinExecutable,
    string ClamWinDatabase,
    int Action,
    bool NotifyReceiver,
    bool NotifySender,
    bool CustomScannerEnabled,
    string CustomScannerExecutable,
    int CustomScannerReturnValue,
    int MaximumMessageSize,
    bool EnableAttachmentBlocking,
    bool ClamAvEnabled,
    string ClamAvHost,
    int ClamAvPort);
