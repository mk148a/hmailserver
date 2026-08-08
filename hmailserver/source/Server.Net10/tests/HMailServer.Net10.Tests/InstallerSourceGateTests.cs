using System.Diagnostics;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class InstallerSourceGateTests
{
    [TestMethod]
    public void InstallerSources_ExistAndPinLegacyPayloadWiring()
    {
        var installerRoot = LocateInstallerRoot();
        var scriptPath = Path.Combine(installerRoot, "hMailServer64.iss");
        var fileSectionPath = Path.Combine(installerRoot, "section_files_64.iss");

        Assert.IsTrue(File.Exists(scriptPath), "hMailServer64.iss missing.");
        Assert.IsTrue(File.Exists(fileSectionPath), "section_files_64.iss missing.");

        var script = File.ReadAllText(scriptPath);
        foreach (var include in new[]
        {
            "section_setup.iss",
            "section_setup_64.iss",
            "section_files_common.iss",
            "section_files_64.iss",
            "hMailServerInnoExtension.iss"
        })
        {
            StringAssert.Contains(script, $"#include \"{include}\"");
            Assert.IsTrue(File.Exists(Path.Combine(installerRoot, include)), $"Missing installer include: {include}");
        }

        var fileSection = File.ReadAllText(fileSectionPath);
        StringAssert.Contains(
            fileSection,
            "..\\source\\server\\hMailServer\\x64\\Release\\hMailServer.exe");
        StringAssert.Contains(
            fileSection,
            "..\\source\\server\\hMailServer\\x64\\Release\\hMailServer.tlb");
    }

    [TestMethod]
    public void InstallerBuild_ProducesArtifactWhenExplicitlyEnabled()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_RUN_INSTALLER_BUILD"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                "Installer build is opt-in; set HMAILSERVER_NET10_RUN_INSTALLER_BUILD=1 on an isolated build host.");
        }

        var installerRoot = LocateInstallerRoot();
        var iscc = FindInnoSetup();
        var legacyReleaseRoot = Path.GetFullPath(Path.Combine(
            installerRoot,
            "..",
            "source",
            "server",
            "hMailServer",
            "x64",
            "Release"));
        var legacyExecutable = Path.Combine(legacyReleaseRoot, "hMailServer.exe");
        if (iscc is null || !File.Exists(legacyExecutable))
        {
            var isccDescription = iscc ?? "missing";
            Assert.Inconclusive(
                $"Installer prerequisites are unavailable. ISCC={isccDescription}; legacy binary={legacyExecutable}.");
        }

        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-net10-installer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = iscc!,
                WorkingDirectory = installerRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("/Qp");
            startInfo.ArgumentList.Add($"/O{outputDirectory}");
            startInfo.ArgumentList.Add(Path.Combine(installerRoot, "hMailServer64.iss"));

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process, "Could not start ISCC.exe.");
            Assert.IsTrue(process.WaitForExit(TimeSpan.FromMinutes(15)), "ISCC.exe timed out.");

            var output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
            Assert.AreEqual(0, process.ExitCode, output);

            var installers = Directory.GetFiles(outputDirectory, "*.exe", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(1, installers.Length, output);
            Assert.IsTrue(new FileInfo(installers[0]).Length > 0, "The generated installer is empty.");
        }
        finally
        {
            try
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Keep a failed build artifact for diagnosis if the tool still holds it open.
            }
        }
    }

    private static string LocateInstallerRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "hmailserver", "installation");
            if (File.Exists(Path.Combine(candidate, "hMailServer64.iss")))
            {
                return candidate;
            }
        }

        Assert.Fail("Could not locate the installer directory from the test output directory.");
        return string.Empty;
    }

    private static string? FindInnoSetup()
    {
        var candidates = new[]
        {
            @"C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
            @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            @"C:\Program Files\Inno Setup 5\ISCC.exe",
            @"C:\Program Files\Inno Setup 6\ISCC.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
