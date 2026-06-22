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
[Guid("122C5B58-9A23-40F5-83C0-7B683D156522")]
public enum ComConnectionSecurity
{
    None = 0,
    Tls = 1,
    StartTlsOptional = 2,
    StartTlsRequired = 3
}
