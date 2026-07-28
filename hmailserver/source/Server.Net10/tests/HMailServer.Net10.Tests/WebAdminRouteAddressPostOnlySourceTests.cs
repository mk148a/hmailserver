using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminRouteAddressPostOnlySourceTests
{
    [TestMethod]
    public void RouteAddressHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_route_address_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != 2", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The route-address handler must retain the domain-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "routeid", "routeaddressid", "action", "routeaddress" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$obSettings->Routes");
        StringAssert.Contains(handler, "$obRoutes->ItemByDBID($routeid)");
        StringAssert.Contains(handler, "$obRoute->Addresses");
        StringAssert.Contains(handler, "$obAddresses->ItemByDBID($routeaddressid)");
        StringAssert.Contains(handler, "$obAddresses->Add()");
        StringAssert.Contains(handler, "$obAddresses->DeleteByDBID($routeaddressid)");
        StringAssert.Contains(handler, "$obAddress->Address = $routeaddress;");
        StringAssert.Contains(handler, "$obAddress->RouteID = $routeid;");
        StringAssert.Contains(handler, "$obAddress->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=route_addresses&routeid=$routeid\");");
    }

    [TestMethod]
    public void RouteAddressMutationFormsUsePostCsrf()
    {
        var editForm = ReadWebAdminSource("hm_route_address.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_route_address_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"routeid\", $routeid)");
        StringAssert.Contains(editForm, "PrintHidden(\"routeaddressid\", $routeaddressid)");

        var addressesPage = ReadWebAdminSource("hm_route_addresses.php");
        var deleteFormStart = addressesPage.IndexOf(
            "echo \"<form action=\\\"index.php\\\" method=\\\"post\\\"",
            StringComparison.Ordinal);
        Assert.IsTrue(deleteFormStart >= 0, "The route-address delete form was not found.");

        var deleteForm = addressesPage.Substring(deleteFormStart);
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_route_address_save\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"routeid\", $routeid)");
        StringAssert.Contains(deleteForm, "PrintHidden(\"routeaddressid\", $routeaddressid)");
    }

    private static string ReadWebAdminSource(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "hmailserver",
                "source",
                "WebAdmin",
                fileName);

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail($"Could not locate hmailserver/source/WebAdmin/{fileName} from the test output directory.");
        return string.Empty;
    }
}
