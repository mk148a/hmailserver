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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private BlockedAttachmentAdministrationSnapshot[]? _blockedAttachments;
    private readonly Func<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>>? _reload;
    private readonly Action<int>? _deleteById;
    private readonly Func<BlockedAttachmentAdministrationSnapshot, int>? _insert;
    private readonly Action<BlockedAttachmentAdministrationSnapshot>? _update;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public BlockedAttachments()
    {
    }

    private BlockedAttachments(
        IReadOnlyList<BlockedAttachmentAdministrationSnapshot> blockedAttachments,
        Func<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>>? reload,
        Func<BlockedAttachmentAdministrationSnapshot, int>? insert,
        Action<BlockedAttachmentAdministrationSnapshot>? update,
        Func<bool>? isServerAdministrator,
        Action<int>? deleteById,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _blockedAttachments = blockedAttachments.ToArray();
        _reload = reload;
        _deleteById = deleteById;
        _insert = insert;
        _update = update;
        _isServerAdministrator = isServerAdministrator;
        _authorizationLeaseFactory = authorizationLeaseFactory;
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

            return BlockedAttachment.CreateAuthorized(
                blockedAttachments[index],
                save: _update is null ? null : SaveExistingAttachment,
                delete: _deleteById is null ? null : DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public void DeleteByDBID(int databaseId)
    {
        var attachments = GetBlockedAttachments();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!attachments.Any(attachment => attachment.Id == databaseId))
        {
            return;
        }

        EnsureServerAdministrator();
        using var authorizationLease = AcquireAuthorizationLease();
        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _blockedAttachments,
                attachments.Where(attachment => attachment.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the blocked attachment from the database.",
                EFail);
        }
    }

    public IInterfaceBlockedAttachment Add()
    {
        _ = GetBlockedAttachments();
        if (_insert is null)
        {
            return Unavailable<IInterfaceBlockedAttachment>();
        }

        return BlockedAttachment.CreateAuthorized(
            new BlockedAttachmentAdministrationSnapshot(0, string.Empty, string.Empty),
            save: SaveNewAttachment,
            delete: _deleteById is null ? null : DeleteByDBID,
            isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceBlockedAttachment get_ItemByDBID(int databaseId)
    {
        var match = GetBlockedAttachments().FirstOrDefault(attachment => attachment.Id == databaseId);

        return match is null
            ? throw new COMException("No blocked attachment with the specified database identifier exists.", DispEBadIndex)
            : BlockedAttachment.CreateAuthorized(
                match,
                save: _update is null ? null : SaveExistingAttachment,
                delete: _deleteById is null ? null : DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
    }

    public void Refresh()
    {
        _ = GetBlockedAttachments();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var blockedAttachments = _reload();
            ArgumentNullException.ThrowIfNull(blockedAttachments);
            Volatile.Write(ref _blockedAttachments, blockedAttachments.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of blocked attachments from the database.",
                EFail);
        }
    }

    internal static BlockedAttachments CreateAuthorized(
        IReadOnlyList<BlockedAttachmentAdministrationSnapshot> blockedAttachments,
        Func<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>>? reload = null,
        Func<BlockedAttachmentAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null,
        Action<BlockedAttachmentAdministrationSnapshot>? update = null,
        Action<int>? deleteById = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(blockedAttachments);
        return new BlockedAttachments(
            blockedAttachments,
            reload,
            insert,
            update,
            isServerAdministrator,
            deleteById,
            authorizationLeaseFactory);
    }

    private BlockedAttachmentAdministrationSnapshot SaveNewAttachment(
        BlockedAttachmentAdministrationSnapshot attachment)
    {
        EnsureServerAdministrator();
        using var authorizationLease = AcquireAuthorizationLease();
        var attachments = GetBlockedAttachments();
        if (_insert is null)
        {
            Unavailable();
        }

        try
        {
            var insertedId = _insert!(attachment);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The blocked attachment insert did not return a valid generated identity.");
            }

            var persisted = attachment with { Id = insertedId };
            Volatile.Write(ref _blockedAttachments, attachments.Append(persisted).ToArray());
            return persisted;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the blocked attachment to the database.",
                EFail);
        }
    }

    private BlockedAttachmentAdministrationSnapshot SaveExistingAttachment(
        BlockedAttachmentAdministrationSnapshot attachment)
    {
        EnsureServerAdministrator();
        using var authorizationLease = AcquireAuthorizationLease();
        var attachments = GetBlockedAttachments();
        if (_update is null)
        {
            Unavailable();
        }

        try
        {
            _update!(attachment);
            Volatile.Write(
                ref _blockedAttachments,
                attachments
                    .Select(existing => existing.Id == attachment.Id ? attachment : existing)
                    .ToArray());
            return attachment;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the blocked attachment to the database.",
                EFail);
        }
    }

    private IReadOnlyList<BlockedAttachmentAdministrationSnapshot> GetBlockedAttachments()
    {
        EnsureServerAdministrator();
        return Volatile.Read(ref _blockedAttachments)
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

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Blocked attachment access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private IDisposable? AcquireAuthorizationLease()
    {
        if (_authorizationLeaseFactory is null)
        {
            return null;
        }

        return _authorizationLeaseFactory(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            ?? throw new COMException(
                "Blocked attachment access requires an authenticated server administrator.",
                EAccessDenied);
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

    private BlockedAttachmentAdministrationSnapshot? _blockedAttachment;
    private readonly Func<BlockedAttachmentAdministrationSnapshot, BlockedAttachmentAdministrationSnapshot>? _save;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public BlockedAttachment()
    {
    }

    private BlockedAttachment(
        BlockedAttachmentAdministrationSnapshot blockedAttachment,
        Func<BlockedAttachmentAdministrationSnapshot, BlockedAttachmentAdministrationSnapshot>? save,
        Action<int>? delete,
        Func<bool>? isServerAdministrator)
    {
        _blockedAttachment = blockedAttachment;
        _save = save;
        _delete = delete;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public string Wildcard { get => Snapshot.Wildcard; set => Mutate(snapshot => snapshot with { Wildcard = value ?? string.Empty }); }

    public string Description { get => Snapshot.Description; set => Mutate(snapshot => snapshot with { Description = value ?? string.Empty }); }

    public void Save()
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _blockedAttachment = _save(Snapshot);
    }

    public void Delete()
    {
        EnsureServerAdministrator();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    internal static BlockedAttachment CreateAuthorized(
        BlockedAttachmentAdministrationSnapshot blockedAttachment,
        Func<BlockedAttachmentAdministrationSnapshot, BlockedAttachmentAdministrationSnapshot>? save = null,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null) =>
        new(blockedAttachment, save, delete, isServerAdministrator);

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

    private void Mutate(
        Func<BlockedAttachmentAdministrationSnapshot, BlockedAttachmentAdministrationSnapshot> mutation)
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _blockedAttachment = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Blocked attachment access requires an authenticated server administrator.",
                EAccessDenied);
        }
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

    internal static BlockedAttachments CreateAuthorizedAdapter(
        Func<bool>? isServerAdministrator = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer blocked attachment administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<BlockedAttachmentAdministrationSnapshot> LoadBlockedAttachments() => store
            .GetBlockedAttachmentsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertBlockedAttachment(BlockedAttachmentAdministrationSnapshot attachment) => store
            .InsertBlockedAttachmentAsync(attachment, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return BlockedAttachments.CreateAuthorized(
            LoadBlockedAttachments(),
            LoadBlockedAttachments,
            InsertBlockedAttachment,
            isServerAdministrator: isServerAdministrator,
            update: UpdateBlockedAttachment,
            deleteById: DeleteBlockedAttachment,
            authorizationLeaseFactory: authorizationLeaseFactory);

        void UpdateBlockedAttachment(BlockedAttachmentAdministrationSnapshot attachment) => store
            .UpdateBlockedAttachmentAsync(attachment, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteBlockedAttachment(int databaseId) => store
            .DeleteBlockedAttachmentByIdAsync(databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}
