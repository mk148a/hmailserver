using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("F8759D53-9D91-47EA-A8C2-A9AF151E1FD4")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDistributionListRecipients
{
    [DispId(0)]
    IInterfaceDistributionListRecipient this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceDistributionListRecipient get_ItemByDBID(int databaseId);

    [DispId(3)]
    IInterfaceDistributionListRecipient Add();

    [DispId(4)]
    void DeleteByDBID(int databaseId);
}

[ComVisible(true)]
[Guid("6DD90CB4-5E1E-45C8-9748-28A020A13E4D")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDistributionListRecipient
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string RecipientAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(3)]
    void Delete();

    [DispId(4)]
    void Save();
}

[ComVisible(true)]
[Guid("AB59F3C1-4904-4F1D-883A-4BFC699A7D0B")]
[ProgId("hMailServer.DistributionListRecipients.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDistributionListRecipients))]
public sealed class DistributionListRecipients : IInterfaceDistributionListRecipients
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DistributionListRecipientAdministrationSnapshot[]? _recipients;
    private readonly Func<DistributionListRecipientAdministrationSnapshot, int>? _insert;
    private readonly Func<DistributionListRecipientAdministrationSnapshot, bool>? _update;
    private readonly int? _owningListId;
    private readonly Func<bool>? _isAuthenticated;

    public DistributionListRecipients()
    {
    }

    private DistributionListRecipients(
        IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients,
        Func<DistributionListRecipientAdministrationSnapshot, int>? insert,
        Func<DistributionListRecipientAdministrationSnapshot, bool>? update,
        int? owningListId,
        Func<bool>? isAuthenticated)
    {
        _recipients = recipients.ToArray();
        _insert = insert;
        _update = update;
        _owningListId = owningListId;
        _isAuthenticated = isAuthenticated;
    }

    public int Count => GetRecipients().Count;

    internal static DistributionListRecipients CreateAuthorized(
        IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients,
        Func<DistributionListRecipientAdministrationSnapshot, int>? insert = null,
        Func<DistributionListRecipientAdministrationSnapshot, bool>? update = null,
        int? owningListId = null,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        return new DistributionListRecipients(recipients, insert, update, owningListId, isAuthenticated);
    }

    public IInterfaceDistributionListRecipient this[int index]
    {
        get
        {
            var recipients = GetRecipients();
            if (index < 0 || index >= recipients.Count)
            {
                throw new COMException("Distribution-list recipient index was outside the collection.", DispEBadIndex);
            }

            return DistributionListRecipient.CreateAuthorized(
                recipients[index],
                update: _update is null ? null : UpdateRecipient,
                isAuthenticated: _isAuthenticated);
        }
    }

    public IInterfaceDistributionListRecipient get_ItemByDBID(int databaseId)
    {
        var match = GetRecipients().FirstOrDefault(recipient => recipient.Id == databaseId);

        return match is null
            ? throw new COMException("No distribution-list recipient with the specified database identifier exists.", DispEBadIndex)
            : DistributionListRecipient.CreateAuthorized(
                match,
                update: _update is null ? null : UpdateRecipient,
                isAuthenticated: _isAuthenticated);
    }

    public IInterfaceDistributionListRecipient Add()
    {
        _ = GetRecipients();
        EnsureAuthenticated();
        if (_insert is null || _owningListId is null || _isAuthenticated is null)
        {
            return Unavailable<IInterfaceDistributionListRecipient>();
        }

        return DistributionListRecipient.CreateAuthorized(
            new DistributionListRecipientAdministrationSnapshot(0, _owningListId.Value, string.Empty),
            save: SaveRecipient,
            isAuthenticated: _isAuthenticated);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    private IReadOnlyList<DistributionListRecipientAdministrationSnapshot> GetRecipients()
    {
        EnsureAuthenticated();
        return Volatile.Read(ref _recipients)
            ?? throw new COMException(
                "DistributionListRecipients access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private DistributionListRecipientAdministrationSnapshot SaveRecipient(
        DistributionListRecipientAdministrationSnapshot recipient)
    {
        EnsureAuthenticated();
        var recipients = GetRecipients();
        if (recipient.Id != 0 || _insert is null || _owningListId is null)
        {
            Unavailable();
            return recipient;
        }

        var ownerScopedRecipient = recipient with { ListId = _owningListId.Value };
        try
        {
            var insertedId = _insert(ownerScopedRecipient);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The distribution-list recipient insert did not return a valid generated identity.");
            }

            var insertedRecipient = ownerScopedRecipient with { Id = insertedId };
            Volatile.Write(ref _recipients, recipients.Append(insertedRecipient).ToArray());
            return insertedRecipient;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the distribution-list recipient to the database.",
                EFail);
        }
    }

    private DistributionListRecipientAdministrationSnapshot UpdateRecipient(
        DistributionListRecipientAdministrationSnapshot recipient)
    {
        EnsureAuthenticated();
        var recipients = GetRecipients();
        if (recipient.Id == 0 || _update is null || _owningListId is null)
        {
            Unavailable();
            return recipient;
        }

        var ownerScopedRecipient = recipient with { ListId = _owningListId.Value };
        try
        {
            if (!_update(ownerScopedRecipient))
            {
                throw new InvalidOperationException(
                    "The distribution-list recipient update did not affect an existing owner-scoped row.");
            }

            var matchingIndex = Array.FindIndex(recipients.ToArray(), current => current.Id == ownerScopedRecipient.Id);
            if (matchingIndex >= 0)
            {
                var replacedRecipients = recipients.ToArray();
                replacedRecipients[matchingIndex] = ownerScopedRecipient;
                Volatile.Write(ref _recipients, replacedRecipients);
            }

            return ownerScopedRecipient;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the distribution-list recipient to the database.",
                EFail);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "DistributionListRecipients access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private T Unavailable<T>()
    {
        _ = GetRecipients();
        throw new COMException(
            "This DistributionListRecipients member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetRecipients();
        throw new COMException(
            "This DistributionListRecipients member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("0886D5D8-4C1C-46F1-BC7B-EEDC9FE9DFFA")]
[ProgId("hMailServer.DistributionListRecipient.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDistributionListRecipient))]
public sealed class DistributionListRecipient : IInterfaceDistributionListRecipient
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DistributionListRecipientAdministrationSnapshot? _recipient;
    private readonly Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot>? _save;
    private readonly Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot>? _update;
    private readonly Func<bool>? _isAuthenticated;

    public DistributionListRecipient()
    {
    }

    private DistributionListRecipient(
        DistributionListRecipientAdministrationSnapshot recipient,
        Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot>? save,
        Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot>? update,
        Func<bool>? isAuthenticated)
    {
        _recipient = recipient;
        _save = save;
        _update = update;
        _isAuthenticated = isAuthenticated;
    }

    public int ID => Snapshot.Id;

    public string RecipientAddress
    {
        get => Snapshot.Address;
        set => Mutate(snapshot => snapshot with { Address = value ?? string.Empty });
    }

    internal static DistributionListRecipient CreateAuthorized(
        DistributionListRecipientAdministrationSnapshot recipient,
        Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot>? save = null,
        Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot>? update = null,
        Func<bool>? isAuthenticated = null) =>
        new(recipient, save, update, isAuthenticated);

    public void Delete() => Unavailable();

    public void Save()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (snapshot.Id == 0)
        {
            if (_save is null)
            {
                Unavailable();
                return;
            }

            _recipient = _save(snapshot);
            return;
        }

        if (_update is null)
        {
            Unavailable();
            return;
        }

        _recipient = _update(snapshot);
    }

    private DistributionListRecipientAdministrationSnapshot Snapshot =>
        _recipient ?? throw new COMException(
            "DistributionListRecipient access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This DistributionListRecipient member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Mutate(
        Func<DistributionListRecipientAdministrationSnapshot, DistributionListRecipientAdministrationSnapshot> mutation)
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (snapshot.Id == 0)
        {
            if (_save is null)
            {
                Unavailable();
                return;
            }

            _recipient = mutation(snapshot);
            return;
        }

        if (_update is null)
        {
            Unavailable();
            return;
        }

        _recipient = mutation(snapshot);
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "DistributionListRecipient access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }
}

[ComVisible(false)]
public static class DistributionListRecipientAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDistributionListRecipientAdministrationStore? _store;

    public static void Configure(IDistributionListRecipientAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static DistributionListRecipients CreateAuthorizedAdapter(
        int distributionListId,
        Func<bool>? isAuthenticated = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer distribution-list recipient administration runtime has not been initialized.",
                CoENotInitialized);

        var recipients = store
            .GetRecipientsAsync(distributionListId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertRecipient(DistributionListRecipientAdministrationSnapshot recipient) => store
            .InsertDistributionListRecipientAsync(recipient, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool UpdateRecipient(DistributionListRecipientAdministrationSnapshot recipient) => store
            .UpdateDistributionListRecipientAsync(recipient, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DistributionListRecipients.CreateAuthorized(
            recipients,
            InsertRecipient,
            UpdateRecipient,
            distributionListId,
            isAuthenticated);
    }
}
