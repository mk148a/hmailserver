using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSessionBrokerContractTests
{
    [TestMethod]
    public void Contract_UsesFreshAdditiveIdentitySet()
    {
        var ids = new[]
        {
            new Guid(WebAdminSessionBrokerContract.TypeLibraryId),
            new Guid(WebAdminSessionBrokerContract.InterfaceId),
            new Guid(WebAdminSessionBrokerContract.ClassId),
            new Guid(WebAdminSessionBrokerContract.AppId)
        };

        CollectionAssert.AreEqual(ids.Distinct().ToArray(), ids);
        Assert.AreNotEqual(new Guid(LegacyComRegistrationManifest.TypeLibraryId), ids[0]);
        Assert.AreNotEqual(new Guid(LegacyComRegistrationManifest.AppId), ids[3]);
        Assert.AreNotEqual(typeof(IInterfaceApplication).GUID, ids[1]);
        Assert.AreNotEqual(typeof(Application).GUID, ids[2]);

        Assert.AreEqual(WebAdminSessionBrokerContract.InterfaceId, typeof(IInterfaceWebAdminSessionBroker).GUID.ToString("D").ToUpperInvariant());
        Assert.AreEqual(
            "hMailServer.WebAdminSessionBroker.1",
            new string(WebAdminSessionBrokerContract.VersionedProgId.ToCharArray()));
        Assert.AreEqual(
            "hMailServer.WebAdminSessionBroker",
            new string(WebAdminSessionBrokerContract.VersionIndependentProgId.ToCharArray()));
    }

    [TestMethod]
    public void Contract_PreservesFrozenDualVtableAndDispids()
    {
        var type = typeof(IInterfaceWebAdminSessionBroker);
        var visible = type.GetCustomAttribute<ComVisibleAttribute>();
        var interfaceType = type.GetCustomAttribute<InterfaceTypeAttribute>();
        var typeLib = type.GetCustomAttribute<TypeLibTypeAttribute>();

        Assert.IsNotNull(visible);
        Assert.IsTrue(visible!.Value);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, interfaceType!.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            typeLib!.Value);

        var members = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(method => method.GetCustomAttribute<DispIdAttribute>()!.Value)
            .Select(method => (method.Name, method.GetCustomAttribute<DispIdAttribute>()!.Value))
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                ("CreateSession", 1),
                ("OpenApplication", 2),
                ("Revoke", 3),
                ("RotateAfterOwnPasswordChange", 4)
            },
            members);
    }

    [TestMethod]
    public void LegacyManifest_DoesNotRegisterTheUnapprovedBroker()
    {
        var manifest = LegacyComRegistrationManifest.Create(
            @"C:\hMailServer\hMailServer.exe",
            @"C:\hMailServer\hMailServer.tlb");

        CollectionAssert.DoesNotContain(
            manifest.UninstallRoots.ToArray(),
            WebAdminSessionBrokerContract.VersionedProgId);
        CollectionAssert.DoesNotContain(
            manifest.UninstallRoots.ToArray(),
            WebAdminSessionBrokerContract.VersionIndependentProgId);
        CollectionAssert.DoesNotContain(
            manifest.UninstallRoots.ToArray(),
            $"AppID\\{{{WebAdminSessionBrokerContract.AppId}}}");
        CollectionAssert.DoesNotContain(
            manifest.UninstallRoots.ToArray(),
            $"TypeLib\\{{{WebAdminSessionBrokerContract.TypeLibraryId}}}");
        Assert.IsFalse(manifest.Values.Any(value =>
            value.Value.Contains(WebAdminSessionBrokerContract.ClassId, StringComparison.OrdinalIgnoreCase)
            || value.Value.Contains(WebAdminSessionBrokerContract.AppId, StringComparison.OrdinalIgnoreCase)
            || value.Value.Contains(WebAdminSessionBrokerContract.TypeLibraryId, StringComparison.OrdinalIgnoreCase)
            || value.Value.Contains(WebAdminSessionBrokerContract.VersionIndependentProgId, StringComparison.OrdinalIgnoreCase)));
    }
}
