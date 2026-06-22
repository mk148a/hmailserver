using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true), Guid("A4C709A3-98B2-410D-84F4-EDA999BF0CB2"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceSettings;

[ComVisible(true), Guid("2CDFD68F-62F2-49CF-A14A-505E7F68EE9C"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceDomains;

[ComVisible(true), Guid("90471F47-FE77-46C7-ADDB-F800B7ED0F66"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceDatabase;

[ComVisible(true), Guid("F6BB0F43-EDEE-49A8-8166-672F3017426F"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceUtilities;

[ComVisible(true), Guid("C3E2DFFB-BE53-4BE6-BE57-7C5609938CEB"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceStatus;

[ComVisible(true), Guid("995F9181-E761-42FA-9057-FE070B37D0F3"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceRules;

[ComVisible(true), Guid("E773E8FC-1C9A-4E96-A73C-CC02E7649637"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceBackupManager;

[ComVisible(true), Guid("F0FE3307-66D9-480D-B1B5-3AF6CD3C0C8A"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceGlobalObjects;

[ComVisible(true), Guid("E252D063-7E86-4FCE-B702-A5E89E0DFB48"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceLinks;

[ComVisible(true), Guid("FB8812B0-524A-4922-9E29-A7E9A9E9151D"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceDiagnostics;

[ComVisible(true), Guid("1259E989-465E-4B63-BB0B-4DB7F6244ACE"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceMessages;

[ComVisible(true), Guid("1517E0BE-5226-46CC-8C2A-BB16B680FF48"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceFetchAccounts;

[ComVisible(true), Guid("328B16A7-8314-4398-B506-90937569EDBA"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceIMAPFolders;
