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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<DistributionListRecipientAdministrationSnapshot>? _recipients;

    public DistributionListRecipients()
    {
    }

    private DistributionListRecipients(IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients)
    {
        _recipients = recipients.ToArray();
    }

    public int Count => GetRecipients().Count;

    internal static DistributionListRecipients CreateAuthorized(
        IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        return new DistributionListRecipients(recipients);
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

            return DistributionListRecipient.CreateAuthorized(recipients[index]);
        }
    }

    public IInterfaceDistributionListRecipient get_ItemByDBID(int databaseId)
    {
        var match = GetRecipients().FirstOrDefault(recipient => recipient.Id == databaseId);

        return match is null
            ? throw new COMException("No distribution-list recipient with the specified database identifier exists.", DispEBadIndex)
            : DistributionListRecipient.CreateAuthorized(match);
    }

    public IInterfaceDistributionListRecipient Add() => Unavailable<IInterfaceDistributionListRecipient>();

    public void DeleteByDBID(int databaseId) => Unavailable();

    private IReadOnlyList<DistributionListRecipientAdministrationSnapshot> GetRecipients()
    {
        return _recipients
            ?? throw new COMException(
                "DistributionListRecipients access requires an authenticated server administrator.",
                EAccessDenied);
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

    private readonly DistributionListRecipientAdministrationSnapshot? _recipient;

    public DistributionListRecipient()
    {
    }

    private DistributionListRecipient(DistributionListRecipientAdministrationSnapshot recipient)
    {
        _recipient = recipient;
    }

    public int ID => Snapshot.Id;

    public string RecipientAddress
    {
        get => Snapshot.Address;
        set => Unavailable();
    }

    internal static DistributionListRecipient CreateAuthorized(
        DistributionListRecipientAdministrationSnapshot recipient) =>
        new(recipient);

    public void Delete() => Unavailable();

    public void Save() => Unavailable();

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

    internal static DistributionListRecipients CreateAuthorizedAdapter(int distributionListId)
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

        return DistributionListRecipients.CreateAuthorized(recipients);
    }
}
