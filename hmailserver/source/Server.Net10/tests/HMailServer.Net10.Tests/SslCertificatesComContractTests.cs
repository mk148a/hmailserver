using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SslCertificatesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceSSLCertificates),
            "A6C0B92B-3973-4E0A-86CB-440AD6C80B71",
            new[]
            {
                "get_Item", "get_Count", "DeleteByDBID", "Add",
                "get_ItemByDBID", "Refresh", "Clear"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceSSLCertificates).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            typeof(IInterfaceSSLCertificates).GetMethod(nameof(IInterfaceSSLCertificates.Clear))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceSSLCertificate),
            "5CB10D83-8FDA-461B-AD5B-3CBBF9476FD6",
            new[]
            {
                "get_ID", "get_Name", "set_Name", "Save",
                "get_CertificateFile", "set_CertificateFile",
                "get_PrivateKeyFile", "set_PrivateKeyFile", "Delete"
            });
        Assert.AreEqual(
            6,
            typeof(IInterfaceSSLCertificate).GetMethod(nameof(IInterfaceSSLCertificate.Delete))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<SSLCertificates>(
            "BE7AF6BB-2ECA-4313-BE00-16A72D82AE49",
            "hMailServer.SSLCertificates.1",
            typeof(IInterfaceSSLCertificates));
        AssertComClass<SSLCertificate>(
            "11A68C45-EC73-496A-A300-2EB8820824EF",
            "hMailServer.SSLCertificate.1",
            typeof(IInterfaceSSLCertificate));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var certificatesError = Assert.ThrowsExactly<COMException>(() => _ = new SSLCertificates().Count);
        var certificateError = Assert.ThrowsExactly<COMException>(() => _ = new SSLCertificate().Name);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().SSLCertificates);

        Assert.AreEqual(EAccessDenied, certificatesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, certificateError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceSSLCertificates certificates = SSLCertificates.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key"),
                Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key")
            });

        Assert.AreEqual(2, certificates.Count);
        AssertCertificate(
            certificates[0],
            10,
            "Alpha certificate",
            @"C:\certs\alpha.crt",
            @"C:\certs\alpha.key");
        AssertCertificate(
            certificates.get_ItemByDBID(20),
            20,
            "Beta certificate",
            @"C:\certs\beta.crt",
            @"C:\certs\beta.key");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = certificates[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(30));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => certificates.DeleteByDBID(10));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => certificates.Add());
        var pendingRefresh = Assert.ThrowsExactly<COMException>(certificates.Refresh);
        var pendingClear = Assert.ThrowsExactly<COMException>(certificates.Clear);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => certificates[0].Name = "Changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(certificates[0].Save);
        var pendingCertificateDelete = Assert.ThrowsExactly<COMException>(certificates[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingClear.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingCertificateDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredSslCertificateRuntime()
    {
        SslCertificateAdministrationRuntimeHost.Configure(
            new FixedSslCertificateAdministrationStore(
                new[]
                {
                    Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key"),
                    Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key")
                }));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var certificates = settings.SSLCertificates;

        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Alpha certificate", certificates[0].Name);
        Assert.AreEqual(@"C:\certs\alpha.crt", certificates[0].CertificateFile);
    }

    private static SslCertificateAdministrationSnapshot Snapshot(
        int id,
        string name,
        string certificateFile,
        string privateKeyFile) =>
        new(id, name, certificateFile, privateKeyFile);

    private static void AssertCertificate(
        IInterfaceSSLCertificate certificate,
        int id,
        string name,
        string certificateFile,
        string privateKeyFile)
    {
        Assert.AreEqual(id, certificate.ID);
        Assert.AreEqual(name, certificate.Name);
        Assert.AreEqual(certificateFile, certificate.CertificateFile);
        Assert.AreEqual(privateKeyFile, certificate.PrivateKeyFile);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private sealed class FixedSslCertificateAdministrationStore(
        IReadOnlyList<SslCertificateAdministrationSnapshot> certificates)
        : ISslCertificateAdministrationStore
    {
        public ValueTask<IReadOnlyList<SslCertificateAdministrationSnapshot>> GetSslCertificatesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<SslCertificateAdministrationSnapshot>>(
                certificates.OrderBy(static certificate => certificate.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
