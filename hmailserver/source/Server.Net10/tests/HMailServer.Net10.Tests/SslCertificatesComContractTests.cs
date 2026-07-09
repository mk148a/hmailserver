using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SslCertificatesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
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
        var certificatesRefreshError = Assert.ThrowsExactly<COMException>(new SSLCertificates().Refresh);
        var certificatesClearError = Assert.ThrowsExactly<COMException>(new SSLCertificates().Clear);
        var certificateError = Assert.ThrowsExactly<COMException>(() => _ = new SSLCertificate().Name);
        var certificateDeleteError = Assert.ThrowsExactly<COMException>(new SSLCertificate().Delete);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().SSLCertificates);

        Assert.AreEqual(EAccessDenied, certificatesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, certificatesRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, certificatesClearError.ErrorCode);
        Assert.AreEqual(EAccessDenied, certificateError.ErrorCode);
        Assert.AreEqual(EAccessDenied, certificateDeleteError.ErrorCode);
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
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceSSLCertificates certificates = SSLCertificates.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key")
            },
            () =>
            {
                reloads++;
                if (failReload)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }

                return new[]
                {
                    Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key"),
                    Snapshot(30, "Gamma certificate", @"C:\certs\gamma.crt", @"C:\certs\gamma.key")
                };
            });

        Assert.AreEqual(1, certificates.Count);
        Assert.AreEqual("Alpha certificate", certificates[0].Name);

        certificates.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, certificates.Count);
        AssertCertificate(
            certificates[0],
            20,
            "Beta certificate",
            @"C:\certs\beta.crt",
            @"C:\certs\beta.key");
        Assert.AreEqual("Gamma certificate", certificates.get_ItemByDBID(30).Name);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(certificates.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Beta certificate", certificates.get_ItemByDBID(20).Name);
    }

    [TestMethod]
    public void AuthorizedCollection_ClearCallsConfiguredOperationAndRetainsSnapshotOnFailure()
    {
        var failClear = true;
        var clears = 0;
        IInterfaceSSLCertificates certificates = SSLCertificates.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key"),
                Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key")
            },
            clear: () =>
            {
                clears++;
                if (failClear)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });

        var clearFailure = Assert.ThrowsExactly<COMException>(certificates.Clear);

        Assert.AreEqual(EFail, clearFailure.ErrorCode);
        Assert.AreEqual(1, clears);
        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Alpha certificate", certificates[0].Name);

        failClear = false;
        certificates.Clear();

        Assert.AreEqual(2, clears);
        Assert.AreEqual(0, certificates.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(10)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDCallsConfiguredOperationAndRetainsSnapshotOnFailure()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceSSLCertificates certificates = SSLCertificates.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key"),
                Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => certificates.DeleteByDBID(10));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Alpha certificate", certificates[0].Name);

        failDelete = false;
        certificates.DeleteByDBID(10);

        CollectionAssert.AreEqual(new[] { 10, 10 }, deletedIds);
        Assert.AreEqual(1, certificates.Count);
        Assert.AreEqual("Beta certificate", certificates[0].Name);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(10)).ErrorCode);

        certificates.DeleteByDBID(999);

        CollectionAssert.AreEqual(new[] { 10, 10, 999 }, deletedIds);
        Assert.AreEqual(1, certificates.Count);
        Assert.AreEqual("Beta certificate", certificates[0].Name);
    }

    [TestMethod]
    public void AuthorizedCollection_ItemDeleteCallsConfiguredOperationAndUpdatesOwningSnapshot()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceSSLCertificates certificates = SSLCertificates.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key"),
                Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });
        var alpha = certificates[0];
        var beta = certificates.get_ItemByDBID(20);

        var deleteFailure = Assert.ThrowsExactly<COMException>(alpha.Delete);

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Alpha certificate", certificates[0].Name);

        failDelete = false;
        alpha.Delete();

        CollectionAssert.AreEqual(new[] { 10, 10 }, deletedIds);
        Assert.AreEqual(1, certificates.Count);
        Assert.AreEqual("Beta certificate", certificates[0].Name);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(10)).ErrorCode);

        beta.Delete();

        CollectionAssert.AreEqual(new[] { 10, 10, 20 }, deletedIds);
        Assert.AreEqual(0, certificates.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(20)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredSslCertificateRuntime()
    {
        var store = new MutableSslCertificateAdministrationStore(
            new[]
            {
                Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key"),
                Snapshot(10, "Alpha certificate", @"C:\certs\alpha.crt", @"C:\certs\alpha.key")
            });
        SslCertificateAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var certificates = settings.SSLCertificates;

        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Alpha certificate", certificates[0].Name);
        Assert.AreEqual(@"C:\certs\alpha.crt", certificates[0].CertificateFile);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(30, "Gamma certificate", @"C:\certs\gamma.crt", @"C:\certs\gamma.key"),
                Snapshot(20, "Beta certificate", @"C:\certs\beta.crt", @"C:\certs\beta.key")
            });

        certificates.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, certificates.Count);
        Assert.AreEqual("Beta certificate", certificates[0].Name);
        Assert.AreEqual(30, certificates.get_ItemByDBID(30).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(10)).ErrorCode);

        certificates.DeleteByDBID(20);

        Assert.AreEqual(1, store.DeletedIds.Count);
        Assert.AreEqual(20, store.DeletedIds[0]);
        Assert.AreEqual(1, certificates.Count);
        Assert.AreEqual(30, certificates[0].ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(20)).ErrorCode);

        certificates[0].Delete();

        Assert.AreEqual(2, store.DeletedIds.Count);
        Assert.AreEqual(30, store.DeletedIds[1]);
        Assert.AreEqual(0, certificates.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(30)).ErrorCode);

        certificates.Clear();

        Assert.AreEqual(1, store.ClearCount);
        Assert.AreEqual(0, certificates.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = certificates.get_ItemByDBID(20)).ErrorCode);
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

    private sealed class MutableSslCertificateAdministrationStore(
        IReadOnlyList<SslCertificateAdministrationSnapshot> certificates)
        : ISslCertificateAdministrationStore
    {
        private IReadOnlyList<SslCertificateAdministrationSnapshot> _certificates = certificates;

        public int ReadCount { get; private set; }

        public int ClearCount { get; private set; }

        public List<int> DeletedIds { get; } = [];

        public void Replace(IReadOnlyList<SslCertificateAdministrationSnapshot> certificates)
        {
            _certificates = certificates;
        }

        public ValueTask<IReadOnlyList<SslCertificateAdministrationSnapshot>> GetSslCertificatesAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<SslCertificateAdministrationSnapshot>>(
                _certificates.OrderBy(static certificate => certificate.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public ValueTask ClearSslCertificatesAsync(CancellationToken cancellationToken)
        {
            ClearCount++;
            _certificates = [];
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteSslCertificateByIdAsync(
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedIds.Add(databaseId);
            _certificates = _certificates
                .Where(certificate => certificate.Id != databaseId)
                .ToArray();
            return ValueTask.CompletedTask;
        }
    }
}
