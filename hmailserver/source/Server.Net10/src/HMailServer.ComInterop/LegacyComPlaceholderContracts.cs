using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true), Guid("D79148F6-78A9-4F60-B8E8-48C33D888FC5"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceRuleCriterias;

[ComVisible(true), Guid("DBFD3E11-9121-4DDD-944B-5AF29BF3D2DF"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceRuleActions;

[ComVisible(true), Guid("FB8812B0-524A-4922-9E29-A7E9A9E9151D"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceDiagnostics;

[ComVisible(true), Guid("1259E989-465E-4B63-BB0B-4DB7F6244ACE"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceMessages;

[ComVisible(true), Guid("CBE3FE9E-3642-4BA1-9BE0-6E766C0DE961"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceIMAPFolderPermissions;

[ComVisible(true), Guid("315BF27F-F832-4FBE-83FE-1C5A5011FAC7"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceRouteAddresses;

[ComVisible(true), Guid("AE45B7CD-C050-4B14-A983-30D53059D24F"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceCache;

[ComVisible(true), Guid("D6B91C3A-90C1-4943-B818-EE66119E4702"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceSURBLServers;

[ComVisible(true), Guid("D8D54486-4CC5-4240-A4BF-DD68D9C3E85B"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceGreyListingWhiteAddresses;

[ComVisible(true), Guid("8492EE2E-7332-4253-B93E-D8B011B47D78"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceWhiteListAddresses;
