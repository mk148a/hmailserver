using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true), Guid("FB8812B0-524A-4922-9E29-A7E9A9E9151D"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceDiagnostics;

[ComVisible(true), Guid("1259E989-465E-4B63-BB0B-4DB7F6244ACE"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceMessages;

[ComVisible(true), Guid("CBE3FE9E-3642-4BA1-9BE0-6E766C0DE961"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IInterfaceIMAPFolderPermissions;
