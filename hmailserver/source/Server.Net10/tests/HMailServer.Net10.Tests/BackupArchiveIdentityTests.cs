using HMailServer.ComInterop;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

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
        var nestedPath = Path.Combine(rawPath, "nested", "deeper");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "nested.eml"), "nested");
        Directory.CreateDirectory(Path.Combine(rawPath, "empty"));
        Directory.CreateDirectory(Path.Combine(rawPath, "nested-empty"));
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
            var snapshotDataBackupPath = Path.GetDirectoryName(snapshotRawPath)!;
            Assert.AreEqual(
                "nested",
                File.ReadAllText(Path.Combine(
                    snapshotDataBackupPath,
                    "nested",
                    "deeper",
                    "nested.eml")));
            Assert.IsTrue(Directory.Exists(Path.Combine(snapshotDataBackupPath, "empty")));
            Assert.IsTrue(Directory.Exists(Path.Combine(snapshotDataBackupPath, "nested-empty")));
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
    public void Binding_FailsOnPreExistingSnapshotDirectoryWithoutDeletingSentinel()
    {
        var root = CreateTestDirectory("hmailserver-binding-collision-");
        var archivePath = Path.Combine(root, "backup.7z");
        var snapshotDirectory = Path.Combine(root, "snapshot");
        Directory.CreateDirectory(snapshotDirectory);
        File.WriteAllText(archivePath, "archive");
        var sentinelPath = Path.Combine(snapshotDirectory, "sentinel.txt");
        File.WriteAllText(sentinelPath, "preserve");

        try
        {
            Assert.ThrowsExactly<IOException>(() =>
                BackupArchiveBinding.TryCreateForTesting(archivePath, root, "snapshot"));

            Assert.AreEqual("preserve", File.ReadAllText(sentinelPath));
            Assert.IsFalse(File.Exists(Path.Combine(snapshotDirectory, "archive.7z")));
            CollectionAssert.AreEqual(
                new[] { "snapshot" },
                Directory.GetDirectories(root)
                    .Select(Path.GetFileName)
                    .OrderBy(name => name)
                    .ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Binding_SecondBindingPreservesTheFirstBinding()
    {
        var root = CreateTestDirectory("hmailserver-binding-duplicate-");
        var archivePath = Path.Combine(root, "backup.7z");
        File.WriteAllText(archivePath, "first");
        BackupArchiveBinding? first = null;

        try
        {
            first = BackupArchiveBinding.TryCreateForTesting(archivePath, root, "snapshot");
            Assert.IsNotNull(first);

            Assert.ThrowsExactly<IOException>(() =>
                BackupArchiveBinding.TryCreateForTesting(archivePath, root, "snapshot"));

            Assert.AreEqual("first", File.ReadAllText(first.ArchivePath));
            Assert.IsTrue(File.Exists(first.ArchivePath));
            CollectionAssert.AreEqual(
                new[] { "snapshot" },
                Directory.GetDirectories(root)
                    .Select(Path.GetFileName)
                    .OrderBy(name => name)
                    .ToArray());
        }
        finally
        {
            first?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Binding_FailedCreationCleansItsOwnedStagingDirectoryRecursively()
    {
        var sourceRoot = CreateTestDirectory("hmailserver-binding-failure-source-");
        var snapshotRoot = CreateTestDirectory("hmailserver-binding-failure-snapshot-");
        var archivePath = Path.Combine(sourceRoot, "backup.7z");
        var rawPath = Path.Combine(sourceRoot, "DataBackup");
        var snapshotDirectory = Path.Combine(snapshotRoot, "snapshot");
        Directory.CreateDirectory(Path.Combine(rawPath, "nested", "deeper"));
        File.WriteAllText(archivePath, "archive");
        File.WriteAllText(Path.Combine(rawPath, "nested", "deeper", "message.eml"), "message");
        Directory.CreateDirectory(snapshotDirectory);
        var sentinelPath = Path.Combine(snapshotDirectory, "sentinel.txt");
        File.WriteAllText(sentinelPath, "preserve");

        try
        {
            Assert.ThrowsExactly<IOException>(() =>
                BackupArchiveBinding.TryCreateForTesting(archivePath, snapshotRoot, "snapshot"));

            Assert.AreEqual("preserve", File.ReadAllText(sentinelPath));
            CollectionAssert.AreEqual(
                new[] { "snapshot" },
                Directory.GetDirectories(snapshotRoot)
                    .Select(Path.GetFileName)
                    .OrderBy(name => name)
                    .ToArray());
        }
        finally
        {
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(snapshotRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Binding_DisposeDeletesOnlyItsOwnedSnapshotDirectory()
    {
        var sourceRoot = CreateTestDirectory("hmailserver-binding-dispose-source-");
        var snapshotRoot = CreateTestDirectory("hmailserver-binding-dispose-snapshot-");
        var archivePath = Path.Combine(sourceRoot, "backup.7z");
        File.WriteAllText(archivePath, "archive");
        BackupArchiveBinding? binding = null;

        try
        {
            binding = BackupArchiveBinding.TryCreateForTesting(archivePath, snapshotRoot, "snapshot");
            Assert.IsNotNull(binding);
            var snapshotDirectory = Path.GetDirectoryName(binding.ArchivePath)!;

            Assert.IsTrue(Directory.Exists(snapshotDirectory));
            binding.Dispose();
            binding = null;

            Assert.IsFalse(Directory.Exists(snapshotDirectory));
            Assert.IsTrue(Directory.Exists(snapshotRoot));
            Assert.AreEqual(0, Directory.GetDirectories(snapshotRoot).Length);
        }
        finally
        {
            binding?.Dispose();
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(snapshotRoot, recursive: true);
        }
    }

    [TestMethod]
    public void RawDataIdentity_PreservesCanonicalDigestForNestedEmptyZeroByteAndUnicodeEntries()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-raw-digest-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var snapshot = Path.Combine(root, "snapshot");
        Directory.CreateDirectory(Path.Combine(source, "empty"));
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllBytes(Path.Combine(source, "nested", "zero.bin"), Array.Empty<byte>());
        File.WriteAllBytes(
            Path.Combine(source, "\u00DCnicode.txt"),
            Encoding.UTF8.GetBytes("\u0434\u0430\u043D\u043D\u044B\u0435"));

        try
        {
            var identity = BackupDataDirectoryIdentity.CopyStableSnapshot(source, snapshot);

            Assert.AreEqual(
                "AACAEBC218DEA987630A27182447D2A2D1A58451422E770E4C90AD5125905A89",
                identity.Sha256);
            Assert.IsTrue(identity.Matches(snapshot));

            File.WriteAllText(Path.Combine(snapshot, "\u00DCnicode.txt"), "tampered");
            Assert.IsFalse(identity.Matches(snapshot));
        }
        finally
        {
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

            var snapshotDirectory = Path.GetDirectoryName(binding.ArchivePath)!;
            var snapshotRoot = Directory.GetParent(snapshotDirectory)!.FullName;
            AssertProtectedForCurrentUserAndSystem(snapshotRoot);
            AssertProtectedForCurrentUserAndSystem(snapshotDirectory);
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

    private static string CreateTestDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool HasFullControl(IEnumerable<FileSystemAccessRule> rules, string sid) =>
        rules.Any(rule =>
            rule.IdentityReference.Value.Equals(sid, StringComparison.OrdinalIgnoreCase)
            && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);

    private static void AssertProtectedForCurrentUserAndSystem(string directory)
    {
        var security = new DirectoryInfo(directory).GetAccessControl();
        var rules = security
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Allow)
            .ToArray();
        var currentUserSid = WindowsIdentity.GetCurrent().User!.Value;

        Assert.IsTrue(security.AreAccessRulesProtected, directory);
        Assert.IsTrue(HasFullControl(rules, currentUserSid), directory);
        Assert.IsTrue(HasFullControl(
            rules,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value),
            directory);
    }

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
