using System.Text.Json;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ReleaseArtifactGateTests
{
    [TestMethod]
    public void ServiceOutput_ContainsRequiredNet10ReleaseArtifacts()
    {
        var serviceOutput = LocateServiceOutput();

        foreach (var artifact in new[]
        {
            "hMailServer.exe",
            "hMailServer.dll",
            "hMailServer.deps.json",
            "hMailServer.runtimeconfig.json",
            "HMailServer.ComInterop.dll",
            "HMailServer.ComInterop.comhost.dll",
            "HMailServer.Core.dll",
            "HMailServer.Protocols.dll",
            "HMailServer.Storage.SqlServer.dll",
            "HMailServer.Delivery.dll",
            "HMailServer.Security.dll",
            "7za.exe",
            "BouncyCastle.Cryptography.dll"
        })
        {
            Assert.IsTrue(File.Exists(Path.Combine(serviceOutput, artifact)), $"Missing release artifact: {artifact}");
        }

        var runtimeConfigPath = Path.Combine(serviceOutput, "hMailServer.runtimeconfig.json");
        using var document = JsonDocument.Parse(File.ReadAllText(runtimeConfigPath));
        var runtimeOptions = document.RootElement.GetProperty("runtimeOptions");
        var target = runtimeOptions.TryGetProperty("framework", out var singleFramework)
            ? singleFramework.GetProperty("name").GetString()
            : runtimeOptions.GetProperty("frameworks")[0].GetProperty("name").GetString();
        Assert.AreEqual("Microsoft.NETCore.App", target);
    }

    private static string LocateServiceOutput()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "hmailserver",
                "source",
                "Server.Net10",
                "src",
                "HMailServer.Service",
                "bin",
                "Debug",
                "net10.0-windows");
            if (File.Exists(Path.Combine(candidate, "hMailServer.exe")))
            {
                return candidate;
            }
        }

        Assert.Fail("Could not locate the Service output directory from the test output directory.");
        return string.Empty;
    }
}