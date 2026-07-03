using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("0CD0DDFF-2D30-41BE-9845-D37EADB1A007")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAttachment;

[ComVisible(true)]
[Guid("BED37911-1180-4840-A831-196C6771EF54")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAttachments;

[ComVisible(true)]
[Guid("9B47C955-4462-48E3-91FE-C5E1CFEC80E0")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRecipients;

[ComVisible(true)]
[Guid("65D57DF8-68A1-4358-BB98-C3B33595B699")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRecipient;

[ComVisible(true)]
[Guid("FF69E250-CBFD-4AB6-9440-39599478365D")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessageHeader;

[ComVisible(true)]
[Guid("1ADE0B5E-536C-4707-8385-32A7F6F92500")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessageHeaders;
