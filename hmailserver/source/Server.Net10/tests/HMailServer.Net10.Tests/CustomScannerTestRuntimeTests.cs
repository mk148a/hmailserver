using System.Diagnostics;
using System.Text;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class CustomScannerTestRuntimeTests
{
    [TestMethod]
    public void TestConnection_ScansCleanAndEicarFilesWithQuotedFileTemplate()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var dataDirectory = Path.Combine(root, "Data Dir");
            var executablePath = Path.Combine(root, "scanner tool.exe");
            var calls = new List<ProcessStartInfo>();
            var payloads = new List<string>();
            var timeouts = new List<TimeSpan>();
            var runtime = new CustomScannerTestRuntime(
                new CustomScannerTestRuntimeOptions
                {
                    DataDirectory = dataDirectory,
                    Timeout = TimeSpan.FromSeconds(2)
                },
                (startInfo, timeout) =>
                {
                    calls.Add(startInfo);
                    timeouts.Add(timeout);
                    payloads.Add(File.ReadAllText(startInfo.ArgumentList[0], Encoding.ASCII));
                    return new CustomScannerProcessResult(
                        true,
                        calls.Count == 1 ? 0 : 7);
                });

            var result = runtime.TestConnection(
                "\"" + executablePath + "\" \"%FILE%\" --mode strict",
                virusReturnCode: 7);

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
                Assert.AreEqual(3, call.ArgumentList.Count);
                Assert.IsTrue(Path.IsPathFullyQualified(call.ArgumentList[0]));
                Assert.AreEqual("--mode", call.ArgumentList[1]);
                Assert.AreEqual("strict", call.ArgumentList[2]);
            }

            Assert.AreEqual(0, Directory.GetFiles(dataDirectory).Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TestConnection_AppendsQuotedFileWhenTemplateHasNoPlaceholder()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var dataDirectory = Path.Combine(root, "Data");
            var executablePath = Path.Combine(root, "scanner.exe");
            ProcessStartInfo? firstCall = null;
            var runtime = new CustomScannerTestRuntime(
                new CustomScannerTestRuntimeOptions
                {
                    DataDirectory = dataDirectory
                },
                (startInfo, _) =>
                {
                    firstCall ??= startInfo;
                    return new CustomScannerProcessResult(true, 7);
                });

            var result = runtime.TestConnection("\"" + executablePath + "\" --scan", virusReturnCode: 7);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("False positive: Unknown", result.ResultText);
            Assert.IsNotNull(firstCall);
            Assert.AreEqual(executablePath, firstCall.FileName);
            Assert.AreEqual("--scan", firstCall.ArgumentList[0]);
            Assert.IsTrue(Path.IsPathFullyQualified(firstCall.ArgumentList[1]));
            Assert.IsTrue(firstCall.ArgumentList[1].EndsWith(".eml", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void BuildCommandLine_PreservesQuotedFileArgumentAsSingleArgument()
    {
        var commandLine = CustomScannerTestRuntime.BuildCommandLine(
            "scanner.exe --file \"%FILE%\"",
            "C:\\Data\\quoted \"name\"\\message.eml");

        var arguments = CustomScannerTestRuntime.SplitWindowsCommandLine(commandLine);

        CollectionAssert.AreEqual(
            new[]
            {
                "scanner.exe",
                "--file",
                "C:\\Data\\quoted \"name\"\\message.eml"
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void TestConnection_ReturnsProcessFailureTextWhenScannerCannotStart()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var calls = 0;
            var runtime = new CustomScannerTestRuntime(
                new CustomScannerTestRuntimeOptions
                {
                    DataDirectory = Path.Combine(root, "Data")
                },
                (_, _) =>
                {
                    calls++;
                    return new CustomScannerProcessResult(
                        false,
                        0,
                        "Unable to launch executable.");
                });

            var result = runtime.TestConnection(@"C:\Missing\scan.cmd %FILE%", virusReturnCode: 7);

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
