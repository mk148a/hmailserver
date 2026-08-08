using HMailServer.ComInterop;

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
