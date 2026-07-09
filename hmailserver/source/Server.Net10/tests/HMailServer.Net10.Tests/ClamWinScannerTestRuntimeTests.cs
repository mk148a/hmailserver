using System.Diagnostics;
using System.Text;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ClamWinScannerTestRuntimeTests
{
    [TestMethod]
    public void TestConnection_ScansCleanAndEicarFilesWithLegacyClamWinArguments()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var dataDirectory = Path.Combine(root, "Data");
            var tempDirectory = Path.Combine(root, "Temp");
            var databaseDirectory = Path.Combine(root, "ClamWinDb");
            Directory.CreateDirectory(databaseDirectory);
            var executablePath = Path.Combine(root, "clamscan.exe");
            var calls = new List<ProcessStartInfo>();
            var payloads = new List<string>();
            var timeouts = new List<TimeSpan>();
            var runtime = new ClamWinScannerTestRuntime(
                new ClamWinScannerTestRuntimeOptions
                {
                    DataDirectory = dataDirectory,
                    TempDirectory = tempDirectory,
                    Timeout = TimeSpan.FromSeconds(2)
                },
                (startInfo, timeout) =>
                {
                    calls.Add(startInfo);
                    timeouts.Add(timeout);
                    payloads.Add(File.ReadAllText(
                        Path.Combine(startInfo.WorkingDirectory!, startInfo.ArgumentList[1]),
                        Encoding.ASCII));
                    return new ClamWinScannerProcessResult(
                        true,
                        calls.Count == 1 ? 0 : 1);
                });

            var result = runtime.TestConnection("\"" + executablePath + "\"", databaseDirectory);

            Assert.IsTrue(result.Succeeded, result.ResultText);
            Assert.AreEqual("Unknown", result.ResultText);
            Assert.AreEqual(2, calls.Count);
            CollectionAssert.AreEqual(
                new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) },
                timeouts.ToArray());
            Assert.AreEqual("Test", payloads[0]);
            StringAssert.Contains(payloads[1], "EICAR-STANDARD-ANTIVIRUS-TEST-FILE");
            foreach (var call in calls)
            {
                Assert.AreEqual(executablePath, call.FileName);
                Assert.AreEqual(dataDirectory, call.WorkingDirectory);
                Assert.IsFalse(call.UseShellExecute);
                Assert.IsTrue(call.CreateNoWindow);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "--database=" + databaseDirectory,
                        call.ArgumentList[1],
                        "--tempdir=" + tempDirectory
                    },
                    call.ArgumentList.ToArray());
                Assert.AreEqual(Path.GetFileName(call.ArgumentList[1]), call.ArgumentList[1]);
            }

            Assert.AreEqual(0, Directory.GetFiles(dataDirectory).Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TestConnection_ReturnsFalsePositiveWhenCleanPayloadIsInfected()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var calls = 0;
            var runtime = new ClamWinScannerTestRuntime(
                new ClamWinScannerTestRuntimeOptions
                {
                    DataDirectory = Path.Combine(root, "Data"),
                    TempDirectory = Path.Combine(root, "Temp")
                },
                (_, _) =>
                {
                    calls++;
                    return new ClamWinScannerProcessResult(true, 1);
                });

            var result = runtime.TestConnection(@"C:\ClamWin\bin\clamscan.exe", @"C:\ClamWin\db");

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("False positive: Unknown", result.ResultText);
            Assert.AreEqual(1, calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TestConnection_ReturnsProcessFailureTextWhenScannerCannotStart()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var calls = 0;
            var runtime = new ClamWinScannerTestRuntime(
                new ClamWinScannerTestRuntimeOptions
                {
                    DataDirectory = Path.Combine(root, "Data"),
                    TempDirectory = Path.Combine(root, "Temp")
                },
                (_, _) =>
                {
                    calls++;
                    return new ClamWinScannerProcessResult(
                        false,
                        0,
                        "Unable to launch executable.");
                });

            var result = runtime.TestConnection(@"C:\Missing\clamscan.exe", @"C:\ClamWin\db");

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Unable to launch executable.", result.ResultText);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-net10-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
