using HMailServer.ComInterop;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupArchiveIdentityTests
{
    [TestMethod]
    public void Capture_MatchesTheLoadedArchiveContent()
    {
        var archivePath = CreateArchive("first");
        try
        {
            var identity = BackupArchiveIdentity.TryCapture(archivePath);

            Assert.IsNotNull(identity);
            Assert.IsTrue(identity.Matches(archivePath));
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void Matches_RejectsReplacementAtTheSamePath()
    {
        var archivePath = CreateArchive("first");
        try
        {
            var identity = BackupArchiveIdentity.TryCapture(archivePath);
            Assert.IsNotNull(identity);

            File.WriteAllText(archivePath, "replacement");

            Assert.IsFalse(identity.Matches(archivePath));
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void Binding_UsesAnIndependentSnapshotAndCleansItUp()
    {
        var archivePath = CreateArchive("first");
        BackupArchiveBinding? binding = null;
        try
        {
            binding = BackupArchiveBinding.TryCreate(archivePath);

            Assert.IsNotNull(binding);
            Assert.AreNotEqual(Path.GetFullPath(archivePath), binding.ArchivePath);
            Assert.AreEqual("first", File.ReadAllText(binding.ArchivePath));

            File.WriteAllText(archivePath, "replacement");
            Assert.AreEqual("first", File.ReadAllText(binding.ArchivePath));
        }
        finally
        {
            var snapshotPath = binding?.ArchivePath;
            binding?.Dispose();
            if (snapshotPath is not null)
            {
                Assert.IsFalse(File.Exists(snapshotPath));
            }

            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void Binding_SnapshotsAndHashesTheRawDataBackupSibling()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-archive-raw-binding-" + Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(root, "backup.7z");
        var rawPath = Path.Combine(root, "DataBackup");
        Directory.CreateDirectory(rawPath);
        File.WriteAllText(archivePath, "archive");
        File.WriteAllText(Path.Combine(rawPath, "message.eml"), "first");
        BackupArchiveBinding? binding = null;
        try
        {
            binding = BackupArchiveBinding.TryCreate(archivePath);

            Assert.IsNotNull(binding);
            Assert.IsNotNull(binding.RawDataBackupIdentity);
            var snapshotRawPath = Path.Combine(
                Path.GetDirectoryName(binding.ArchivePath)!,
                "DataBackup",
                "message.eml");
            Assert.AreEqual("first", File.ReadAllText(snapshotRawPath));
            Assert.IsTrue(binding.RawDataBackupIdentity.Matches(Path.GetDirectoryName(snapshotRawPath)!));

            File.WriteAllText(Path.Combine(rawPath, "message.eml"), "replacement");
            Assert.AreEqual("first", File.ReadAllText(snapshotRawPath));

            File.WriteAllText(snapshotRawPath, "tampered");
            Assert.IsFalse(binding.RawDataBackupIdentity.Matches(Path.GetDirectoryName(snapshotRawPath)!));
        }
        finally
        {
            binding?.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Binding_ProtectsPrivateSnapshotForCurrentUserAndSystem()
    {
        var archivePath = CreateArchive("protected");
        try
        {
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);

            var snapshotDirectory = new DirectoryInfo(Path.GetDirectoryName(binding.ArchivePath)!);
            var security = snapshotDirectory.GetAccessControl();
            var rules = security
                .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Where(rule => rule.AccessControlType == AccessControlType.Allow)
                .ToArray();
            var currentUserSid = WindowsIdentity.GetCurrent().User!.Value;

            Assert.IsTrue(security.AreAccessRulesProtected);
            Assert.IsTrue(HasFullControl(rules, currentUserSid));
            Assert.IsTrue(HasFullControl(
                rules,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value));
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void Binding_RejectsReparsePointAtPrivateSnapshotRoot()
    {
        var archivePath = CreateArchive("root-reparse");
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-binding-root-{Guid.NewGuid():N}");
        var target = Path.Combine(Path.GetTempPath(), $"hmailserver-binding-root-target-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(target);
            try
            {
                Directory.CreateSymbolicLink(root, target);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("The test host does not allow creating a disposable root reparse point: " + exception.Message);
            }

            Assert.ThrowsExactly<IOException>(() =>
                BackupArchiveBinding.TryCreateForTesting(archivePath, root, "snapshot"));
            Assert.IsFalse(File.Exists(Path.Combine(target, "snapshot", "archive.7z")));
        }
        finally
        {
            TryDeleteReparseOrDirectory(root);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void Binding_RejectsReparsePointAtPrivateSnapshotDirectory()
    {
        var archivePath = CreateArchive("child-reparse");
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-binding-child-{Guid.NewGuid():N}");
        var target = Path.Combine(Path.GetTempPath(), $"hmailserver-binding-child-target-{Guid.NewGuid():N}");
        var child = Path.Combine(root, "snapshot");
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(target);
            try
            {
                Directory.CreateSymbolicLink(child, target);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("The test host does not allow creating a disposable child reparse point: " + exception.Message);
            }

            Assert.ThrowsExactly<IOException>(() =>
                BackupArchiveBinding.TryCreateForTesting(archivePath, root, "snapshot"));
            Assert.IsFalse(File.Exists(Path.Combine(target, "archive.7z")));
        }
        finally
        {
            TryDeleteReparseOrDirectory(child);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void Manager_LoadBackupReadsTheSnapshotBeforeMetadataParsing()
    {
        var archivePath = CreateArchive("first");
        try
        {
            var reader = new ReplacingMetadataReader(archivePath);
            var manager = BackupManager.CreateAuthorized(reader);
            var backup = (Backup)manager.LoadBackup(archivePath);

            Assert.AreEqual(2, reader.Options);
            Assert.AreNotEqual(Path.GetFullPath(archivePath), reader.ArchivePath);
            Assert.AreEqual("first", File.ReadAllText(backup.ArchivePath));
            backup.CleanupArchiveBinding();
            Assert.IsFalse(File.Exists(backup.ArchivePath));
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    private static string CreateArchive(string contents)
    {
        var archivePath = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-archive-identity-" + Guid.NewGuid().ToString("N") + ".7z");
        File.WriteAllText(archivePath, contents);
        return archivePath;
    }

    private static bool HasFullControl(IEnumerable<FileSystemAccessRule> rules, string sid) =>
        rules.Any(rule =>
            rule.IdentityReference.Value.Equals(sid, StringComparison.OrdinalIgnoreCase)
            && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);

    private static void TryDeleteReparseOrDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // The test cleanup must not follow a disposable reparse target.
        }
    }

    private sealed class ReplacingMetadataReader(string sourcePath) : IBackupArchiveMetadataReader
    {
        public string? ArchivePath { get; private set; }

        public int Options { get; private set; }

        public int ReadContainsOptions(string archivePath)
        {
            ArchivePath = archivePath;
            Options = File.ReadAllText(archivePath) == "first" ? 2 : 0;
            File.WriteAllText(sourcePath, "replacement");
            return Options;
        }
    }
}
