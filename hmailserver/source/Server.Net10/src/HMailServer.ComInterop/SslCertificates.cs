using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("A6C0B92B-3973-4E0A-86CB-440AD6C80B71")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSSLCertificates
{
    [DispId(0)]
    IInterfaceSSLCertificate this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceSSLCertificate Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceSSLCertificate get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();

    [DispId(7)]
    void Clear();
}

[ComVisible(true)]
[Guid("5CB10D83-8FDA-461B-AD5B-3CBBF9476FD6")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSSLCertificate
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    void Save();

    [DispId(4)]
    string CertificateFile
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(5)]
    string PrivateKeyFile
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(6)]
    void Delete();
}

[ComVisible(true)]
[Guid("BE7AF6BB-2ECA-4313-BE00-16A72D82AE49")]
[ProgId("hMailServer.SSLCertificates.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSSLCertificates))]
public sealed class SSLCertificates : IInterfaceSSLCertificates
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private SslCertificateAdministrationSnapshot[]? _certificates;
    private readonly Func<IReadOnlyList<SslCertificateAdministrationSnapshot>>? _reload;
    private readonly Action? _clear;
    private readonly Action<int>? _deleteById;

    public SSLCertificates()
    {
    }

    private SSLCertificates(
        IReadOnlyList<SslCertificateAdministrationSnapshot> certificates,
        Func<IReadOnlyList<SslCertificateAdministrationSnapshot>>? reload,
        Action? clear,
        Action<int>? deleteById)
    {
        _certificates = certificates.ToArray();
        _reload = reload;
        _clear = clear;
        _deleteById = deleteById;
    }

    public int Count => GetCertificates().Count;

    internal static SSLCertificates CreateAuthorized(
        IReadOnlyList<SslCertificateAdministrationSnapshot> certificates,
        Func<IReadOnlyList<SslCertificateAdministrationSnapshot>>? reload = null,
        Action? clear = null,
        Action<int>? deleteById = null)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        return new SSLCertificates(certificates, reload, clear, deleteById);
    }

    public IInterfaceSSLCertificate this[int index]
    {
        get
        {
            var certificates = GetCertificates();
            if (index < 0 || index >= certificates.Count)
            {
                throw new COMException("SSL certificate index was outside the collection.", DispEBadIndex);
            }

            return CreateCertificate(certificates[index]);
        }
    }

    public IInterfaceSSLCertificate get_ItemByDBID(int databaseId)
    {
        var match = GetCertificates().FirstOrDefault(certificate => certificate.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No SSL certificate with the specified database identifier exists.",
                DispEBadIndex)
            : CreateCertificate(match);
    }

    public void DeleteByDBID(int databaseId)
    {
        var certificates = GetCertificates();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _certificates,
                certificates.Where(certificate => certificate.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the SSL certificate from the database.",
                EFail);
        }
    }

    public IInterfaceSSLCertificate Add() => Unavailable<IInterfaceSSLCertificate>();

    public void Refresh()
    {
        _ = GetCertificates();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var certificates = _reload();
            ArgumentNullException.ThrowIfNull(certificates);
            Volatile.Write(ref _certificates, certificates.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of SSL certificates from the database.",
                EFail);
        }
    }

    public void Clear()
    {
        _ = GetCertificates();
        if (_clear is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _clear();
            Volatile.Write(ref _certificates, []);
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to clear the SSL certificate list from the database.",
                EFail);
        }
    }

    private IReadOnlyList<SslCertificateAdministrationSnapshot> GetCertificates()
    {
        return Volatile.Read(ref _certificates)
            ?? throw new COMException(
                "SSLCertificates access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private SSLCertificate CreateCertificate(SslCertificateAdministrationSnapshot certificate)
    {
        return SSLCertificate.CreateAuthorized(
            certificate,
            _deleteById is null ? null : DeleteByDBID);
    }

    private T Unavailable<T>()
    {
        _ = GetCertificates();
        throw new COMException(
            "This SSLCertificates member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetCertificates();
        throw new COMException(
            "This SSLCertificates member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("11A68C45-EC73-496A-A300-2EB8820824EF")]
[ProgId("hMailServer.SSLCertificate.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSSLCertificate))]
public sealed class SSLCertificate : IInterfaceSSLCertificate
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly SslCertificateAdministrationSnapshot? _certificate;
    private readonly Action<int>? _delete;

    public SSLCertificate()
    {
    }

    private SSLCertificate(SslCertificateAdministrationSnapshot certificate, Action<int>? delete)
    {
        _certificate = certificate;
        _delete = delete;
    }

    public int ID => Snapshot.Id;

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public string CertificateFile { get => Snapshot.CertificateFile; set => Unavailable(); }

    public string PrivateKeyFile { get => Snapshot.PrivateKeyFile; set => Unavailable(); }

    internal static SSLCertificate CreateAuthorized(
        SslCertificateAdministrationSnapshot certificate,
        Action<int>? delete = null) =>
        new(certificate, delete);

    public void Save() => Unavailable();

    public void Delete()
    {
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    private SslCertificateAdministrationSnapshot Snapshot =>
        _certificate ?? throw new COMException(
            "SSLCertificate access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This SSLCertificate member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class SslCertificateAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static ISslCertificateAdministrationStore? _store;

    public static void Configure(ISslCertificateAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static SSLCertificates CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer SSL certificate administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<SslCertificateAdministrationSnapshot> LoadCertificates() => store
            .GetSslCertificatesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void ClearCertificates() => store
            .ClearSslCertificatesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteCertificateById(int databaseId) => store
            .DeleteSslCertificateByIdAsync(databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return SSLCertificates.CreateAuthorized(
            LoadCertificates(),
            LoadCertificates,
            ClearCertificates,
            DeleteCertificateById);
    }
}
