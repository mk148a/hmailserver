using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("BF5CBCFF-CD54-4FAB-AE60-ADFA9C961C1A")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceBlockedAttachment
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string Wildcard
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    void Save();

    [DispId(4)]
    string Description
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(5)]
    void Delete();
}

[ComVisible(true)]
[Guid("8979F461-AD9D-49E8-8068-BBAB43FBA31A")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceBlockedAttachments
{
    [DispId(0)]
    IInterfaceBlockedAttachment this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceBlockedAttachment Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceBlockedAttachment get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();
}

[ComVisible(true)]
[Guid("1E93E771-45C1-4CAD-9BF6-5D79723C9CBE")]
[ProgId("hMailServer.BlockedAttachments.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceBlockedAttachments))]
public sealed class BlockedAttachments : IInterfaceBlockedAttachments
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<BlockedAttachmentAdministrationSnapshot>? _blockedAttachments;

    public BlockedAttachments()
    {
    }

    private BlockedAttachments(IReadOnlyList<BlockedAttachmentAdministrationSnapshot> blockedAttachments)
    {
        _blockedAttachments = blockedAttachments.ToArray();
    }

    public int Count => GetBlockedAttachments().Count;

    public IInterfaceBlockedAttachment this[int index]
    {
        get
        {
            var blockedAttachments = GetBlockedAttachments();
            if (index < 0 || index >= blockedAttachments.Count)
            {
                throw new COMException("Blocked attachment index was outside the collection.", DispEBadIndex);
            }

            return BlockedAttachment.CreateAuthorized(blockedAttachments[index]);
        }
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceBlockedAttachment Add() => Unavailable<IInterfaceBlockedAttachment>();

    public IInterfaceBlockedAttachment get_ItemByDBID(int databaseId)
    {
        var match = GetBlockedAttachments().FirstOrDefault(attachment => attachment.Id == databaseId);

        return match is null
            ? throw new COMException("No blocked attachment with the specified database identifier exists.", DispEBadIndex)
            : BlockedAttachment.CreateAuthorized(match);
    }

    public void Refresh() => Unavailable();

    internal static BlockedAttachments CreateAuthorized(IReadOnlyList<BlockedAttachmentAdministrationSnapshot> blockedAttachments)
    {
        ArgumentNullException.ThrowIfNull(blockedAttachments);
        return new BlockedAttachments(blockedAttachments);
    }

    private IReadOnlyList<BlockedAttachmentAdministrationSnapshot> GetBlockedAttachments()
    {
        return _blockedAttachments
            ?? throw new COMException(
                "BlockedAttachments access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Unavailable()
    {
        _ = GetBlockedAttachments();
        throw new COMException(
            "This BlockedAttachments member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}

[ComVisible(true)]
[Guid("773BCF69-C1C2-48CD-A8F8-E89A1F74E4B3")]
[ProgId("hMailServer.BlockedAttachment.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceBlockedAttachment))]
public sealed class BlockedAttachment : IInterfaceBlockedAttachment
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly BlockedAttachmentAdministrationSnapshot? _blockedAttachment;

    public BlockedAttachment()
    {
    }

    private BlockedAttachment(BlockedAttachmentAdministrationSnapshot blockedAttachment)
    {
        _blockedAttachment = blockedAttachment;
    }

    public int ID => Snapshot.Id;

    public string Wildcard { get => Snapshot.Wildcard; set => Unavailable(); }

    public string Description { get => Snapshot.Description; set => Unavailable(); }

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    internal static BlockedAttachment CreateAuthorized(BlockedAttachmentAdministrationSnapshot blockedAttachment) =>
        new(blockedAttachment);

    private BlockedAttachmentAdministrationSnapshot Snapshot =>
        _blockedAttachment ?? throw new COMException(
            "BlockedAttachment access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This BlockedAttachment member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class BlockedAttachmentAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IBlockedAttachmentAdministrationStore? _store;

    public static void Configure(IBlockedAttachmentAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static BlockedAttachments CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer blocked attachment administration runtime has not been initialized.",
                CoENotInitialized);

        var blockedAttachments = store
            .GetBlockedAttachmentsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return BlockedAttachments.CreateAuthorized(blockedAttachments);
    }
}
