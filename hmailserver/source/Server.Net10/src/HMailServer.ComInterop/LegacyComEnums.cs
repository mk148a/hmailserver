using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD01")]
public enum ComAdminLevel
{
    Normal = 0,
    DomainAdministrator = 1,
    ServerAdministrator = 2
}

[ComVisible(true)]
[Guid("0005B084-4C3A-11D9-8530-B8CDE3157849")]
public enum ComServerState
{
    Unknown = 0,
    Stopped = 1,
    Starting = 2,
    Running = 3,
    Stopping = 4
}

[ComVisible(true)]
[Guid("027282DE-4C3A-11D9-93CE-D4EDF9405FEE")]
public enum ComLogDevice
{
    Unknown = 0,
    Sql = 1,
    File = 2
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD00")]
public enum ComLogOutputFormat
{
    Default = 1,
    Csa = 2
}

[ComVisible(true)]
[Guid("FD97B388-4C39-11D9-8361-94B829D736A2")]
public enum ComAntivirusAction
{
    DeleteEmail = 0,
    DeleteAttachments = 1
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD03")]
public enum ComRuleMatchType
{
    Unknown = 0,
    Equals = 1,
    Contains = 2,
    LessThan = 3,
    GreaterThan = 4,
    RegExMatch = 5,
    NotContains = 6,
    NotEquals = 7,
    Wildcard = 8
}

[ComVisible(true)]
[Guid("87FDF5A8-567E-4BDD-B5E0-4742D4801A92")]
public enum ComMaintenanceOperation
{
    UpdateImapFolderUid = 1
}

[ComVisible(true)]
[Guid("F58B6982-4C39-11D9-B629-F87B01E1264F")]
public enum ComDatabaseType
{
    Unknown = 0,
    MySql = 1,
    MSSQL = 2,
    PostgreSql = 3,
    MSSQLCE = 4
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD07")]
public enum ComSessionType
{
    Unknown = 0,
    Smtp = 1,
    SmtpClient = 2,
    Pop3 = 3,
    Pop3Client = 4,
    Imap = 5
}

[ComVisible(true)]
[Guid("122C5B58-9A23-40F5-83C0-7B683D156522")]
public enum ComConnectionSecurity
{
    None = 0,
    Tls = 1,
    StartTlsOptional = 2,
    StartTlsRequired = 3
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD08")]
public enum ComDomainSignatureMethod
{
    Unknown = 0,
    SetIfNotSpecifiedInAccount = 1,
    OverwriteAccountSignature = 2,
    AppendToAccountSignature = 3
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD13")]
public enum ComDkimCanonicalizationMethod
{
    Simple = 1,
    Relaxed = 2
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD14")]
public enum ComDkimAlgorithm
{
    SHA1 = 1,
    SHA256 = 2
}

[ComVisible(true)]
[Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD06")]
public enum ComDistributionListMode
{
    Public = 0,
    Membership = 1,
    Announcement = 2,
    DomainMembers = 3,
    ServerMembers = 4
}
