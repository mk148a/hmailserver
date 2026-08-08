using System.Diagnostics;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreExecutionTests
{
    private const string ArchiveXml = """
        <Backup>
          <BackupInformation Mode="2" />
          <Domains>
            <Domain Name="restore.example" Active="1" Postmaster="postmaster@restore.example"
                    MaxMessageSize="1024" UsePlusAddressing="1" PlusAddressingChar="+"
                    AntiSpamOptions="1" MaxNoOfAccounts="2" MaxNoOfAliases="1" MaxNoOfLists="1"
                    LimitationsEnabled="0" EnableSignature="0" SignatureMethod="1" MaxAccountSize="0"
                    MaxSize="0">
              <Accounts>
                <Account Name="alice@restore.example" Active="1" Password="enc" PasswordEncryption="1"
                         AdminLevel="0" MaxAccountSize="128" />
              </Accounts>
              <Aliases>
                <Alias Name="info@restore.example" Value="alice@restore.example" Active="1" />
              </Aliases>
              <DistributionLists>
                <DistributionList Name="team@restore.example" Active="1" RequiresAuth="0"
                                  RequiresAuthAddress="" ListMode="0">
                  <Recipients>
                    <Recipient Name="recipient@example.test" />
                  </Recipients>
                </DistributionList>
              </DistributionLists>
            </Domain>
          </Domains>
        </Backup>
        """;

    [TestMethod]
    public async Task ExecuteAsync_RestoresOnlyQueuedMetadataSections()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        var executor = stores.CreateExecutor(fixture.DataDirectory);
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, stores.Domains.Items.Count);
        Assert.AreEqual("restore.example", stores.Domains.Items[0].Name);
        Assert.AreEqual(1, stores.Accounts.Items.Count);
        Assert.AreEqual(1, stores.Aliases.Items.Count);
        Assert.AreEqual(1, stores.DistributionLists.Items.Count);
        Assert.AreEqual(1, stores.Recipients.Items.Count);
        Assert.AreEqual(0, stores.Domains.Deleted.Count);
        Assert.AreEqual(0, stores.Accounts.Deleted.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsUnsupportedRestoreSelectionBeforeArchiveOrWrites()
    {
        var stores = new RecordingStores();
        var executor = stores.CreateExecutor(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var backup = Backup.CreateAuthorized(2, Path.Combine(Path.GetTempPath(), "missing.7z"));
        backup.RestoreSettings = true;

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        StringAssert.Contains(error.Message, "Only RestoreDomains");
        Assert.AreEqual(0, stores.Domains.Items.Count);
        Assert.AreEqual(0, stores.Accounts.Items.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_RollsBackEveryInsertedMetadataRowOnFailure()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores { FailAliasInsert = true };
        var executor = stores.CreateExecutor(fixture.DataDirectory);
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        CollectionAssert.AreEqual(new[] { 1 }, stores.Domains.Deleted.ToArray());
        CollectionAssert.AreEqual(new[] { 1 }, stores.Accounts.Deleted.Select(static item => item.AccountId).ToArray());
        Assert.AreEqual(0, stores.Aliases.Deleted.Count);
        Assert.AreEqual(0, stores.DistributionLists.Deleted.Count);
        Assert.AreEqual(0, stores.Recipients.Deleted.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_RollsBackRecipientUsingGeneratedId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var archiveXml = ArchiveXml.Replace(
            "</DistributionLists>",
            "            <DistributionList Name=\"second@restore.example\" Active=\"1\" RequiresAuth=\"0\"\n"
                + "                                  RequiresAuthAddress=\"\" ListMode=\"0\" />\n"
                + "          </DistributionLists>",
            StringComparison.Ordinal);
        using var fixture = await ArchiveFixture.CreateAsync(archiveXml);
        var stores = new RecordingStores { FailDistributionListInsertAfterFirst = true };
        var executor = stores.CreateExecutor(fixture.DataDirectory);
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        CollectionAssert.AreEqual(new[] { (1, 1) }, stores.Recipients.Deleted.ToArray());
        CollectionAssert.AreEqual(new[] { (1, 1) }, stores.DistributionLists.Deleted.ToArray());
        CollectionAssert.AreEqual(new[] { 1 }, stores.Domains.Deleted.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RawNonDbRestoreStagesDataAndMetadata()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: false);
        var stores = new RecordingStores();
        var executor = stores.CreateExecutor(fixture.DataDirectory);
        var backup = Backup.CreateAuthorized(6, fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, stores.Domains.Items.Count);
        Assert.AreEqual("restore.example", stores.Domains.Items[0].Name);
        Assert.AreEqual("restored", await File.ReadAllTextAsync(fixture.RestoredFilePath));
        Assert.IsTrue(File.Exists(fixture.RawDataBackupFilePath));
        Assert.IsFalse(File.Exists(fixture.OriginalFilePath));
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedNonDbRestoreStagesDataAndMetadata()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: true);
        var stores = new RecordingStores();
        var executor = stores.CreateExecutor(fixture.DataDirectory);
        var backup = Backup.CreateAuthorized(14, fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, stores.Domains.Items.Count);
        Assert.AreEqual("restore.example", stores.Domains.Items[0].Name);
        Assert.AreEqual("restored", await File.ReadAllTextAsync(fixture.RestoredFilePath));
        Assert.IsFalse(File.Exists(fixture.OriginalFilePath));
    }

    [TestMethod]
    public async Task ExecuteAsync_NonDbMetadataFailureRestoresOriginalDataDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: false);
        var stores = new RecordingStores { FailAliasInsert = true };
        var executor = stores.CreateExecutor(fixture.DataDirectory);
        var backup = Backup.CreateAuthorized(6, fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual("original", await File.ReadAllTextAsync(fixture.OriginalFilePath));
        Assert.IsFalse(File.Exists(fixture.RestoredFilePath));
        Assert.AreEqual(0, stores.Domains.Items.Count);
        CollectionAssert.AreEqual(new[] { 1 }, stores.Domains.Deleted.ToArray());
    }

    private sealed class RecordingStores
    {
        public RecordingDomainStore Domains { get; } = new();
        public RecordingAccountStore Accounts { get; } = new();
        public RecordingAliasStore Aliases { get; } = new();
        public RecordingDistributionListStore DistributionLists { get; } = new();
        public RecordingRecipientStore Recipients { get; } = new();
        public bool FailAliasInsert { get; init; }
        public bool FailDistributionListInsertAfterFirst { get; init; }

        public MetadataBackupRestoreExecutor CreateExecutor(string dataDirectory)
        {
            DistributionLists.FailInsertAfterFirst = FailDistributionListInsertAfterFirst;
            return new(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                Domains,
                Accounts,
                FailAliasInsert ? new FailingAliasStore(Aliases) : Aliases,
                DistributionLists,
                Recipients);
        }
    }

    private sealed class RecordingDomainStore : IDomainAdministrationStore
    {
        public List<DomainAdministrationSnapshot> Items { get; } = [];
        public List<int> Deleted { get; } = [];

        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>(Items.ToArray());

        public ValueTask<int> InsertDomainAsync(DomainAdministrationSnapshot snapshot, CancellationToken cancellationToken)
        {
            var id = Items.Count + 1;
            Items.Add(snapshot with { Id = id });
            return ValueTask.FromResult(id);
        }

        public ValueTask<bool> DeleteDomainByIdAsync(int domainId, CancellationToken cancellationToken)
        {
            Deleted.Add(domainId);
            Items.RemoveAll(item => item.Id == domainId);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class RecordingAccountStore : IAccountAdministrationStore
    {
        public List<AccountAdministrationSnapshot> Items { get; } = [];
        public List<(int DomainId, int AccountId)> Deleted { get; } = [];

        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(Items.Where(item => item.DomainId == domainId).ToArray());

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(int accountId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<AccountAdministrationSnapshot?>(Items.SingleOrDefault(item => item.Id == accountId));

        public ValueTask<int> InsertAccountAsync(int domainId, AccountAdministrationSnapshot snapshot, string password, CancellationToken cancellationToken)
        {
            var id = Items.Count + 1;
            Items.Add(snapshot with { Id = id, DomainId = domainId });
            return ValueTask.FromResult(id);
        }

        public ValueTask<bool> DeleteAccountAsync(int domainId, int accountId, CancellationToken cancellationToken)
        {
            Deleted.Add((domainId, accountId));
            Items.RemoveAll(item => item.Id == accountId);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class RecordingAliasStore : IAliasAdministrationStore
    {
        public List<AliasAdministrationSnapshot> Items { get; } = [];
        public List<(int DomainId, int AliasId)> Deleted { get; } = [];

        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AliasAdministrationSnapshot>>(Items.Where(item => item.DomainId == domainId).ToArray());

        public ValueTask<int> InsertAliasAsync(int owningDomainId, AliasAdministrationSnapshot snapshot, CancellationToken cancellationToken)
        {
            var id = Items.Count + 1;
            Items.Add(snapshot with { Id = id, DomainId = owningDomainId });
            return ValueTask.FromResult(id);
        }

        public ValueTask UpdateAliasAsync(int owningDomainId, AliasAdministrationSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAliasAsync(int owningDomainId, int aliasId, CancellationToken cancellationToken)
        {
            Deleted.Add((owningDomainId, aliasId));
            Items.RemoveAll(item => item.Id == aliasId);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class FailingAliasStore(RecordingAliasStore inner) : IAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(int domainId, CancellationToken cancellationToken) =>
            inner.GetAliasesAsync(domainId, cancellationToken);

        public ValueTask<int> InsertAliasAsync(int owningDomainId, AliasAdministrationSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.FromException<int>(new InvalidOperationException("Simulated alias restore failure."));

        public ValueTask UpdateAliasAsync(int owningDomainId, AliasAdministrationSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAliasAsync(int owningDomainId, int aliasId, CancellationToken cancellationToken) =>
            inner.DeleteAliasAsync(owningDomainId, aliasId, cancellationToken);
    }

    private sealed class RecordingDistributionListStore : IDistributionListAdministrationStore
    {
        public List<DistributionListAdministrationSnapshot> Items { get; } = [];
        public List<(int DomainId, int ListId)> Deleted { get; } = [];
        public bool FailInsertAfterFirst { get; set; }

        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(Items.Where(item => item.DomainId == domainId).ToArray());

        public ValueTask<int> InsertDistributionListAsync(DistributionListAdministrationSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (FailInsertAfterFirst && Items.Count > 0)
            {
                return ValueTask.FromException<int>(new InvalidOperationException("Simulated distribution-list restore failure."));
            }

            var id = Items.Count + 1;
            Items.Add(snapshot with { Id = id });
            return ValueTask.FromResult(id);
        }

        public ValueTask<bool> DeleteDistributionListAsync(int owningDomainId, int distributionListId, CancellationToken cancellationToken)
        {
            Deleted.Add((owningDomainId, distributionListId));
            Items.RemoveAll(item => item.Id == distributionListId);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class RecordingRecipientStore : IDistributionListRecipientAdministrationStore
    {
        public List<DistributionListRecipientAdministrationSnapshot> Items { get; } = [];
        public List<(int ListId, int RecipientId)> Deleted { get; } = [];

        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(int distributionListId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>(Items.Where(item => item.ListId == distributionListId).ToArray());

        public ValueTask<int> InsertDistributionListRecipientAsync(DistributionListRecipientAdministrationSnapshot snapshot, CancellationToken cancellationToken)
        {
            var id = Items.Count + 1;
            Items.Add(snapshot with { Id = id });
            return ValueTask.FromResult(id);
        }

        public ValueTask<bool> DeleteDistributionListRecipientAsync(DistributionListRecipientAdministrationSnapshot snapshot, CancellationToken cancellationToken)
        {
            Deleted.Add((snapshot.ListId, snapshot.Id));
            Items.RemoveAll(item => item.Id == snapshot.Id);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class ArchiveFixture : IDisposable
    {
        private ArchiveFixture(string root, string archivePath, string dataDirectory)
        {
            Root = root;
            ArchivePath = archivePath;
            DataDirectory = dataDirectory;
        }

        public string Root { get; }
        public string ArchivePath { get; }
        public string DataDirectory { get; }
        public string OriginalFilePath => Path.Combine(DataDirectory, "original.txt");
        public string RestoredFilePath => Path.Combine(DataDirectory, "restored.txt");
        public string RawDataBackupFilePath => Path.Combine(Root, "DataBackup", "restored.txt");

        public static async Task<ArchiveFixture> CreateAsync(string xml)
        {
            var root = Path.Combine(Path.GetTempPath(), $"hmailserver-restore-execution-{Guid.NewGuid():N}");
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), xml);
            var archivePath = Path.Combine(root, "backup.7z");
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                WorkingDirectory = source,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("a");
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add("hMailServerBackup.xml");
            startInfo.ArgumentList.Add("-t7z");
            startInfo.ArgumentList.Add("-mx1");
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.AreEqual(0, process.ExitCode, error);
            return new ArchiveFixture(root, archivePath, dataDirectory);
        }

        public static async Task<ArchiveFixture> CreateNonDbAsync(bool compressed)
        {
            var root = Path.Combine(Path.GetTempPath(), $"hmailserver-restore-nondb-{Guid.NewGuid():N}");
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");

            var dataBackup = compressed
                ? Path.Combine(source, "DataBackup")
                : Path.Combine(root, "DataBackup");
            Directory.CreateDirectory(dataBackup);
            File.WriteAllText(Path.Combine(dataBackup, "restored.txt"), "restored");

            var format = compressed ? "7z" : "Raw";
            var mode = compressed ? "14" : "6";
            var xml = ArchiveXml.Replace(
                "<BackupInformation Mode=\"2\" />",
                $"<BackupInformation Mode=\"{mode}\"><DataFiles Format=\"{format}\" FolderName=\"DataBackup\" /></BackupInformation>",
                StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), xml);

            var archivePath = Path.Combine(root, "backup.7z");
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                WorkingDirectory = source,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("a");
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add("hMailServerBackup.xml");
            if (compressed)
            {
                startInfo.ArgumentList.Add("DataBackup");
            }

            startInfo.ArgumentList.Add("-t7z");
            startInfo.ArgumentList.Add("-mx1");
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.AreEqual(0, process.ExitCode, error);
            return new ArchiveFixture(root, archivePath, dataDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
