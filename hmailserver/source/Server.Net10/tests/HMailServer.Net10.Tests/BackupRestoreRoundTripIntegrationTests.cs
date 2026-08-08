using System.Data;
using System.Diagnostics;
using System.Globalization;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BackupRestoreRoundTripIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    private const string NonDbArchiveXml = """
        <Backup>
          <BackupInformation Mode="6">
            <DataFiles Format="Raw" FolderName="DataBackup" />
          </BackupInformation>
          <Domains>
            <Domain Name="roundtrip.example" Active="1" Postmaster="pm@roundtrip.example"
                    MaxMessageSize="1024" UsePlusAddressing="1" PlusAddressingChar="+"
                    AntiSpamOptions="1" MaxNoOfAccounts="2" MaxNoOfAliases="1" MaxNoOfLists="1"
                    LimitationsEnabled="0" EnableSignature="0" SignatureMethod="1" MaxAccountSize="0"
                    MaxSize="0">
              <Accounts>
                <Account Name="user@roundtrip.example" Active="1" Password="enc" PasswordEncryption="1"
                         AdminLevel="1" MaxAccountSize="128" />
              </Accounts>
              <Aliases>
                <Alias Name="alias@roundtrip.example" Value="target@example.test" Active="1" />
              </Aliases>
              <DistributionLists>
                <DistributionList Name="team@roundtrip.example" Active="1" RequiresAuth="0"
                                  RequiresAuthAddress="" ListMode="0">
                  <Recipients>
                    <Recipient Name="r1@example.test" />
                  </Recipients>
                </DistributionList>
              </DistributionLists>
            </Domain>
          </Domains>
        </Backup>
        """;

    private const string ArchiveXml = """
        <Backup>
          <Domains>
            <Domain Name="roundtrip.example" Active="1" Postmaster="pm@roundtrip.example"
                    MaxMessageSize="1024" UsePlusAddressing="1" PlusAddressingChar="+"
                    AntiSpamOptions="1" MaxNoOfAccounts="2" MaxNoOfAliases="1" MaxNoOfLists="1"
                    LimitationsEnabled="0" EnableSignature="0" SignatureMethod="1" MaxAccountSize="0"
                    MaxSize="0">
              <Accounts>
                <Account Name="user@roundtrip.example" Active="1" Password="enc" PasswordEncryption="1"
                         AdminLevel="1" MaxAccountSize="128" />
              </Accounts>
              <Aliases>
                <Alias Name="alias@roundtrip.example" Value="target@example.test" Active="1" />
              </Aliases>
              <DistributionLists>
                <DistributionList Name="team@roundtrip.example" Active="1" RequiresAuth="0"
                                  RequiresAuthAddress="" ListMode="0">
                  <Recipients>
                    <Recipient Name="r1@example.test" />
                  </Recipients>
                </DistributionList>
              </DistributionLists>
            </Domain>
          </Domains>
        </Backup>
        """;

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreMetadata_RoundTripsArchiveIntoIsolatedTargetDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_roundtrip_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var factory = new SqlServerConnectionFactory(testConnectionString);
            var domainStore = new SqlServerDomainAdministrationStore(factory);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var aliasStore = new SqlServerAliasAdministrationStore(factory);
            var listStore = new SqlServerDistributionListAdministrationStore(factory);
            var recipientStore = new SqlServerDistributionListRecipientAdministrationStore(factory);

            var domains = BackupArchiveXmlSnapshotParser.ParseDomains(ArchiveXml);
            var accounts = BackupArchiveXmlSnapshotParser.ParseAccounts(ArchiveXml, domainId: 1);
            var aliases = BackupArchiveXmlSnapshotParser.ParseAliases(ArchiveXml, domainId: 1);
            var lists = BackupArchiveXmlSnapshotParser.ParseDistributionLists(ArchiveXml, domainId: 1);
            var recipients = BackupArchiveXmlSnapshotParser.ParseDistributionListRecipients(ArchiveXml, distributionListId: 1);

            Func<ValueTask> rollback = () => default;
            await BackupRestoreMetadataWriter.RestoreDomainsAsync(domains, domainStore, rollback, CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreAccountsAsync(accounts, domainId: 1, accountStore, rollback, CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreAliasesAsync(aliases, domainId: 1, aliasStore, rollback, CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreDistributionListsAsync(lists, domainId: 1, listStore, rollback, CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreDistributionListRecipientsAsync(recipients, distributionListId: 1, recipientStore, rollback, CancellationToken.None).ConfigureAwait(false);

            var restoredDomain = (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("roundtrip.example", restoredDomain.Name);
            Assert.IsTrue(restoredDomain.Active);

            var restoredAccount = (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("user@roundtrip.example", restoredAccount.Address);
            Assert.AreEqual(128, restoredAccount.MaxSize);

            var restoredAlias = (await aliasStore.GetAliasesAsync(1, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("alias@roundtrip.example", restoredAlias.Name);
            Assert.AreEqual("target@example.test", restoredAlias.Value);

            var restoredList = (await listStore.GetDistributionListsAsync(1, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("team@roundtrip.example", restoredList.Address);
            Assert.IsTrue(restoredList.Active);

            var restoredRecipients = await recipientStore.GetRecipientsAsync(1, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, restoredRecipients.Count);
            Assert.AreEqual("r1@example.test", restoredRecipients[0].Address);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RestoresBoundRawDataAndMetadataIntoDisposableTargets()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_executor_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-executor-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            var dataBackup = Path.Combine(root, "DataBackup");
            var archivePath = Path.Combine(root, "backup.7z");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(dataBackup);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(dataBackup, "restored.txt"), "restored");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), NonDbArchiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                new SqlServerDomainAdministrationStore(factory),
                new SqlServerAccountAdministrationStore(factory),
                new SqlServerAliasAdministrationStore(factory),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory));
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);
            var backup = Backup.CreateAuthorized(
                6,
                binding.ArchivePath,
                archiveIdentity: binding.Identity,
                archiveBinding: binding,
                rawDataBackupIdentity: binding.RawDataBackupIdentity);
            backup.RestoreDomains = true;
            backup.RestoreMessages = true;

            await executor.ExecuteAsync(backup, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("restored", await File.ReadAllTextAsync(Path.Combine(dataDirectory, "restored.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "original.txt")));
            var restoredDomains = await new SqlServerDomainAdministrationStore(factory)
                .GetDomainsAsync(CancellationToken.None)
                .ConfigureAwait(false);
            Assert.AreEqual("roundtrip.example", restoredDomains.Single().Name);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RollsBackSqlAndDataOnMetadataFailure()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_executor_failure_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-executor-failure-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            var dataBackup = Path.Combine(root, "DataBackup");
            var archivePath = Path.Combine(root, "backup.7z");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(dataBackup);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(dataBackup, "restored.txt"), "restored");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), NonDbArchiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            var domainStore = new SqlServerDomainAdministrationStore(factory);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var aliasStore = new SqlServerAliasAdministrationStore(factory);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                domainStore,
                accountStore,
                new FailingAliasAdministrationStore(aliasStore),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory));
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);
            var backup = Backup.CreateAuthorized(
                6,
                binding.ArchivePath,
                archiveIdentity: binding.Identity,
                archiveBinding: binding,
                rawDataBackupIdentity: binding.RawDataBackupIdentity);
            backup.RestoreDomains = true;
            backup.RestoreMessages = true;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

            Assert.AreEqual("original", await File.ReadAllTextAsync(Path.Combine(dataDirectory, "original.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "restored.txt")));
            Assert.AreEqual(0, (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RollsBackDistributionListWhenRecipientRestoreFails()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_executor_recipient_failure_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-executor-recipient-failure-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            var dataBackup = Path.Combine(root, "DataBackup");
            var archivePath = Path.Combine(root, "backup.7z");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(dataBackup);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(dataBackup, "restored.txt"), "restored");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), NonDbArchiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            var domainStore = new SqlServerDomainAdministrationStore(factory);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var aliasStore = new SqlServerAliasAdministrationStore(factory);
            var listStore = new SqlServerDistributionListAdministrationStore(factory);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                domainStore,
                accountStore,
                aliasStore,
                listStore,
                new FailingRecipientAdministrationStore(
                    new SqlServerDistributionListRecipientAdministrationStore(factory)));
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);
            var backup = Backup.CreateAuthorized(
                6,
                binding.ArchivePath,
                archiveIdentity: binding.Identity,
                archiveBinding: binding,
                rawDataBackupIdentity: binding.RawDataBackupIdentity);
            backup.RestoreDomains = true;
            backup.RestoreMessages = true;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

            Assert.AreEqual("original", await File.ReadAllTextAsync(Path.Combine(dataDirectory, "original.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "restored.txt")));
            Assert.AreEqual(0, (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await aliasStore.GetAliasesAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await listStore.GetDistributionListsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RollsBackInsertedRecipientWhenSecondRecipientFails()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_executor_second_recipient_failure_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-executor-second-recipient-failure-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            var dataBackup = Path.Combine(root, "DataBackup");
            var archivePath = Path.Combine(root, "backup.7z");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(dataBackup);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(dataBackup, "restored.txt"), "restored");
            var archiveXml = NonDbArchiveXml.Replace(
                "<Recipient Name=\"r1@example.test\" />",
                "<Recipient Name=\"r1@example.test\" />\n                    <Recipient Name=\"r2@example.test\" />",
                StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), archiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            var domainStore = new SqlServerDomainAdministrationStore(factory);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var aliasStore = new SqlServerAliasAdministrationStore(factory);
            var listStore = new SqlServerDistributionListAdministrationStore(factory);
            var recipientStore = new SqlServerDistributionListRecipientAdministrationStore(factory);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                domainStore,
                accountStore,
                aliasStore,
                listStore,
                new FailingOnSecondRecipientAdministrationStore(recipientStore));
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);
            var backup = Backup.CreateAuthorized(
                6,
                binding.ArchivePath,
                archiveIdentity: binding.Identity,
                archiveBinding: binding,
                rawDataBackupIdentity: binding.RawDataBackupIdentity);
            backup.RestoreDomains = true;
            backup.RestoreMessages = true;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

            Assert.AreEqual("original", await File.ReadAllTextAsync(Path.Combine(dataDirectory, "original.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "restored.txt")));
            Assert.AreEqual(0, (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await aliasStore.GetAliasesAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await listStore.GetDistributionListsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await recipientStore.GetRecipientsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_CommitsDbOnlyMetadataInOneTransaction()
    {
        await WithDbOnlyRestoreTargetAsync(
            "db_only_transaction",
            ToDbOnlyArchiveXml(NonDbArchiveXml),
            async fixture =>
            {
                var executor = fixture.CreateExecutor();
                await executor.ExecuteAsync(fixture.Backup, CancellationToken.None).ConfigureAwait(false);

                Assert.AreEqual(1, (await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(1, (await fixture.AccountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(1, (await fixture.AliasStore.GetAliasesAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(1, (await fixture.ListStore.GetDistributionListsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(1, (await fixture.RecipientStore.GetRecipientsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RollsBackDbOnlyMetadataWhenAliasInsertFails()
    {
        await WithDbOnlyRestoreTargetAsync(
            "db_only_alias_failure",
            ToDbOnlyArchiveXml(NonDbArchiveXml),
            async fixture =>
            {
                var executor = fixture.CreateExecutor(
                    new FailingMetadataTransactionFactory(fixture.TransactionFactory, failAlias: true));

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => executor.ExecuteAsync(fixture.Backup, CancellationToken.None).AsTask());

                await AssertAllMetadataTablesEmptyAsync(fixture).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RollsBackDbOnlyMetadataWhenSecondRecipientInsertFails()
    {
        var archiveXml = ToDbOnlyArchiveXml(
            NonDbArchiveXml.Replace(
                "<Recipient Name=\"r1@example.test\" />",
                "<Recipient Name=\"r1@example.test\" />\n                    <Recipient Name=\"r2@example.test\" />",
                StringComparison.Ordinal));
        await WithDbOnlyRestoreTargetAsync(
            "db_only_second_recipient_failure",
            archiveXml,
            async fixture =>
            {
                var executor = fixture.CreateExecutor(
                    new FailingMetadataTransactionFactory(fixture.TransactionFactory, failSecondRecipient: true));

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => executor.ExecuteAsync(fixture.Backup, CancellationToken.None).AsTask());

                await AssertAllMetadataTablesEmptyAsync(fixture).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_FailsClosedWhenDbOnlyTransactionFactoryIsMissing()
    {
        await WithDbOnlyRestoreTargetAsync(
            "db_only_missing_transaction_factory",
            ToDbOnlyArchiveXml(NonDbArchiveXml),
            async fixture =>
            {
                var executor = fixture.CreateExecutorWithoutTransactionFactory();

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => executor.ExecuteAsync(fixture.Backup, CancellationToken.None).AsTask());

                await AssertAllMetadataTablesEmptyAsync(fixture).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task SqlServerBackupRestoreMetadataTransaction_DisposalWithoutCommitRollsBack()
    {
        await WithDbOnlyRestoreTargetAsync(
            "db_only_disposal_rollback",
            ToDbOnlyArchiveXml(NonDbArchiveXml),
            async fixture =>
            {
                var domain = BackupArchiveXmlSnapshotParser.ParseDomains(NonDbArchiveXml).Single();
                await using (var transaction = await fixture.TransactionFactory
                    .BeginAsync(CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    _ = await transaction.DomainStore.InsertDomainAsync(
                        domain,
                        CancellationToken.None).ConfigureAwait(false);
                }

                Assert.AreEqual(0, (await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task SqlServerBackupRestoreMetadataTransaction_UnsupportedMethodsFailClosed()
    {
        await WithDbOnlyRestoreTargetAsync(
            "unsupported_methods_fail_closed",
            ToDbOnlyArchiveXml(NonDbArchiveXml),
            async fixture =>
            {
                var domain = BackupArchiveXmlSnapshotParser.ParseDomains(NonDbArchiveXml).Single();
                var account = BackupArchiveXmlSnapshotParser.ParseAccounts(NonDbArchiveXml, domainId: 1).Single().Account;
                var alias = BackupArchiveXmlSnapshotParser.ParseAliases(NonDbArchiveXml, domainId: 1).Single();
                var distributionList = BackupArchiveXmlSnapshotParser
                    .ParseDistributionLists(NonDbArchiveXml, domainId: 1)
                    .Single();
                var recipient = BackupArchiveXmlSnapshotParser
                    .ParseDistributionListRecipients(NonDbArchiveXml, distributionListId: 1)
                    .Single();

                await using var transaction = await fixture.TransactionFactory
                    .BeginAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                Assert.AreEqual(
                    0,
                    (await transaction.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);

                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.DomainStore.DeleteDomainByIdAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    async () =>
                    {
                        _ = await transaction.DomainStore.UpdateDomainAsync(
                            domain,
                            CancellationToken.None).ConfigureAwait(false);
                    });

                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.AccountStore.GetAccountsAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.AccountStore.GetAccountByIdAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    () => ((IBackupAccountAdministrationStore)transaction.AccountStore)
                        .GetBackupAccountsAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    async () =>
                    {
                        _ = await transaction.AccountStore.UpdateAccountAsync(
                            1,
                            account,
                            password: null,
                            cancellationToken: CancellationToken.None).ConfigureAwait(false);
                    });
                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.AccountStore.DeleteAccountAsync(1, 1, CancellationToken.None));

                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.AliasStore.GetAliasesAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.AliasStore.UpdateAliasAsync(1, alias, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.AliasStore.DeleteAliasAsync(1, 1, CancellationToken.None));

                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.DistributionListStore.GetDistributionListsAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    async () =>
                    {
                        _ = await transaction.DistributionListStore.UpdateDistributionListAsync(
                            distributionList,
                            CancellationToken.None).ConfigureAwait(false);
                    });
                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.DistributionListStore.DeleteDistributionListAsync(1, 1, CancellationToken.None));

                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.RecipientStore.GetRecipientsAsync(1, CancellationToken.None));
                await AssertTransactionScopedOperationFailsAsync(
                    async () =>
                    {
                        _ = await transaction.RecipientStore.UpdateDistributionListRecipientAsync(
                            recipient,
                            CancellationToken.None).ConfigureAwait(false);
                    });
                await AssertTransactionScopedOperationFailsAsync(
                    () => transaction.RecipientStore.DeleteDistributionListRecipientAsync(
                        recipient,
                        CancellationToken.None));
            }).ConfigureAwait(false);
    }

    private static async Task AssertTransactionScopedOperationFailsAsync(Func<ValueTask> operation)
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => operation().AsTask()).ConfigureAwait(false);
        StringAssert.Contains(exception.Message, "transaction-scoped");
    }

    private static async Task AssertTransactionScopedOperationFailsAsync<T>(Func<ValueTask<T>> operation)
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => operation().AsTask()).ConfigureAwait(false);
        StringAssert.Contains(exception.Message, "transaction-scoped");
    }

    private static async Task AssertAllMetadataTablesEmptyAsync(DbOnlyRestoreFixture fixture)
    {
        Assert.AreEqual(0, (await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
        Assert.AreEqual(0, (await fixture.AccountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        Assert.AreEqual(0, (await fixture.AliasStore.GetAliasesAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        Assert.AreEqual(0, (await fixture.ListStore.GetDistributionListsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        Assert.AreEqual(0, (await fixture.RecipientStore.GetRecipientsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
    }

    private static string ToDbOnlyArchiveXml(string archiveXml) =>
        archiveXml
            .Replace("Mode=\"6\"", "Mode=\"2\"", StringComparison.Ordinal)
            .Replace("<DataFiles Format=\"Raw\" FolderName=\"DataBackup\" />", string.Empty, StringComparison.Ordinal);

    private static async Task WithDbOnlyRestoreTargetAsync(
        string name,
        string archiveXml,
        Func<DbOnlyRestoreFixture, Task> action)
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_{name}_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-{name}-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            var archivePath = Path.Combine(root, "backup.7z");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), archiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);
            var backup = Backup.CreateAuthorized(
                2,
                binding.ArchivePath,
                archiveIdentity: binding.Identity,
                archiveBinding: binding,
                rawDataBackupIdentity: binding.RawDataBackupIdentity);
            backup.RestoreDomains = true;
            await action(new DbOnlyRestoreFixture(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                backup,
                new SqlServerDomainAdministrationStore(factory),
                new SqlServerAccountAdministrationStore(factory),
                new SqlServerAliasAdministrationStore(factory),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                new SqlServerBackupRestoreMetadataTransactionFactory(factory))).ConfigureAwait(false);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class DbOnlyRestoreFixture(
        string sevenZipExecutablePath,
        string dataDirectory,
        Backup backup,
        SqlServerDomainAdministrationStore domainStore,
        SqlServerAccountAdministrationStore accountStore,
        SqlServerAliasAdministrationStore aliasStore,
        SqlServerDistributionListAdministrationStore listStore,
        SqlServerDistributionListRecipientAdministrationStore recipientStore,
        SqlServerBackupRestoreMetadataTransactionFactory transactionFactory)
    {
        internal Backup Backup { get; } = backup;
        internal SqlServerDomainAdministrationStore DomainStore { get; } = domainStore;
        internal SqlServerAccountAdministrationStore AccountStore { get; } = accountStore;
        internal SqlServerAliasAdministrationStore AliasStore { get; } = aliasStore;
        internal SqlServerDistributionListAdministrationStore ListStore { get; } = listStore;
        internal SqlServerDistributionListRecipientAdministrationStore RecipientStore { get; } = recipientStore;
        internal SqlServerBackupRestoreMetadataTransactionFactory TransactionFactory { get; } = transactionFactory;

        internal MetadataBackupRestoreExecutor CreateExecutor(
            IBackupRestoreMetadataTransactionFactory? transactionFactory = null) =>
            new(
                sevenZipExecutablePath,
                dataDirectory,
                DomainStore,
                AccountStore,
                AliasStore,
                ListStore,
                RecipientStore,
                metadataTransactionFactory: transactionFactory ?? TransactionFactory);

        internal MetadataBackupRestoreExecutor CreateExecutorWithoutTransactionFactory() =>
            new(
                sevenZipExecutablePath,
                dataDirectory,
                DomainStore,
                AccountStore,
                AliasStore,
                ListStore,
                RecipientStore,
                metadataTransactionFactory: null,
                requireSqlTransaction: true);
    }

    private sealed class FailingMetadataTransactionFactory(
        IBackupRestoreMetadataTransactionFactory inner,
        bool failAlias = false,
        bool failSecondRecipient = false) : IBackupRestoreMetadataTransactionFactory
    {
        public async ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await inner.BeginAsync(cancellationToken).ConfigureAwait(false);
            return new FailingMetadataTransaction(transaction, failAlias, failSecondRecipient);
        }
    }

    private sealed class FailingMetadataTransaction(
        IBackupRestoreMetadataTransaction inner,
        bool failAlias,
        bool failSecondRecipient) : IBackupRestoreMetadataTransaction
    {
        public IDomainAdministrationStore DomainStore => inner.DomainStore;
        public IAccountAdministrationStore AccountStore => inner.AccountStore;
        public IAliasAdministrationStore AliasStore => failAlias
            ? new FailingAliasAdministrationStore(inner.AliasStore)
            : inner.AliasStore;
        public IDistributionListAdministrationStore DistributionListStore => inner.DistributionListStore;
        public IDistributionListRecipientAdministrationStore RecipientStore => failSecondRecipient
            ? new FailingOnSecondRecipientAdministrationStore(inner.RecipientStore)
            : inner.RecipientStore;
        public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllDomainsForRestoreAsync(cancellationToken);
        public ValueTask CommitAsync(CancellationToken cancellationToken) => inner.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private static async Task CreateArchiveAsync(string archivePath, string sourcePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            WorkingDirectory = sourcePath,
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
        await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        Assert.AreEqual(0, process.ExitCode, error);
    }

    private sealed class FailingAliasAdministrationStore(IAliasAdministrationStore inner) : IAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
            int domainId,
            CancellationToken cancellationToken) => inner.GetAliasesAsync(domainId, cancellationToken);

        public ValueTask<int> InsertAliasAsync(
            int owningDomainId,
            AliasAdministrationSnapshot alias,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<int>(new InvalidOperationException("Simulated alias restore failure."));

        public ValueTask UpdateAliasAsync(
            int owningDomainId,
            AliasAdministrationSnapshot alias,
            CancellationToken cancellationToken) =>
            inner.UpdateAliasAsync(owningDomainId, alias, cancellationToken);

        public ValueTask<bool> DeleteAliasAsync(
            int owningDomainId,
            int aliasId,
            CancellationToken cancellationToken) =>
            inner.DeleteAliasAsync(owningDomainId, aliasId, cancellationToken);
    }

    private sealed class FailingRecipientAdministrationStore(
        IDistributionListRecipientAdministrationStore inner) : IDistributionListRecipientAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken) => inner.GetRecipientsAsync(distributionListId, cancellationToken);

        public ValueTask<int> InsertDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<int>(new InvalidOperationException("Simulated recipient restore failure."));

        public ValueTask<bool> UpdateDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            inner.UpdateDistributionListRecipientAsync(snapshot, cancellationToken);

        public ValueTask<bool> DeleteDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            inner.DeleteDistributionListRecipientAsync(snapshot, cancellationToken);
    }

    private sealed class FailingOnSecondRecipientAdministrationStore(
        IDistributionListRecipientAdministrationStore inner) : IDistributionListRecipientAdministrationStore
    {
        private int _insertCount;

        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken) => inner.GetRecipientsAsync(distributionListId, cancellationToken);

        public ValueTask<int> InsertDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            if (++_insertCount == 2)
            {
                return ValueTask.FromException<int>(new InvalidOperationException("Simulated second recipient restore failure."));
            }

            return inner.InsertDistributionListRecipientAsync(snapshot, cancellationToken);
        }

        public ValueTask<bool> UpdateDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            inner.UpdateDistributionListRecipientAsync(snapshot, cancellationToken);

        public ValueTask<bool> DeleteDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            inner.DeleteDistributionListRecipientAsync(snapshot, cancellationToken);
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var allowDatabaseCreate = Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(rawConnectionString) || !string.Equals(allowDatabaseCreate, "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(rawConnectionString);
        }
        catch (ArgumentException exception)
        {
            Assert.Inconclusive($"The SQL integration connection string is invalid: {exception.Message}");
            throw;
        }

        if (!IsApprovedLocalDataSource(builder.DataSource) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive(
                "The SQL integration fixture only accepts a local SQL/LocalDB target without AttachDbFilename.");
        }

        return builder.ConnectionString;
    }

    private static bool IsApprovedLocalDataSource(string dataSource)
    {
        var normalized = dataSource.Trim();
        return normalized.Equals(".", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateTargetSchemaAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE dbo.hm_domains (
                domainid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                domainname nvarchar(255) NOT NULL,
                domainactive tinyint NOT NULL,
                domainpostmaster nvarchar(255) NOT NULL,
                domainmaxmessagesize int NOT NULL,
                domainuseplusaddressing tinyint NOT NULL,
                domainplusaddressingchar nvarchar(1) NOT NULL,
                domainaddomain nvarchar(255) NOT NULL,
                domainmaxsize int NOT NULL,
                domainmaxnoofaccounts int NOT NULL,
                domainmaxnoofaliases int NOT NULL,
                domainmaxnoofdistributionlists int NOT NULL,
                domainlimitationsenabled tinyint NOT NULL,
                domainmaxaccountsize int NOT NULL,
                domainenablesignature tinyint NOT NULL,
                domainsignaturemethod tinyint NOT NULL,
                domainsignatureplaintext nvarchar(max) NOT NULL,
                domainsignaturehtml nvarchar(max) NOT NULL,
                domainaddsignaturestoreplies tinyint NOT NULL,
                domainaddsignaturestolocalemail tinyint NOT NULL,
                domainantispamoptions int NOT NULL,
                domaindkimselector nvarchar(255) NOT NULL,
                domaindkimprivatekeyfile nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_accounts (
                accountid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                accountdomainid int NOT NULL,
                accountaddress nvarchar(255) NOT NULL,
                accountpassword nvarchar(255) NOT NULL,
                accountactive tinyint NOT NULL,
                accountisad tinyint NOT NULL,
                accountaddomain nvarchar(255) NOT NULL,
                accountadusername nvarchar(255) NOT NULL,
                accountmaxsize int NOT NULL,
                accountvacationmessageon tinyint NOT NULL,
                accountvacationmessage nvarchar(1000) NOT NULL,
                accountvacationsubject nvarchar(200) NOT NULL,
                accountvacationexpires tinyint NOT NULL,
                accountvacationexpiredate nvarchar(255) NOT NULL,
                accountvacationabortspamflagged tinyint NOT NULL,
                accountpwencryption tinyint NOT NULL,
                accountadminlevel tinyint NOT NULL,
                accountforwardenabled tinyint NOT NULL,
                accountforwardaddress nvarchar(255) NOT NULL,
                accountforwardkeeporiginal tinyint NOT NULL,
                accountforwardabortspamflagged tinyint NOT NULL,
                accountenablesignature tinyint NOT NULL,
                accountsignatureplaintext nvarchar(max) NOT NULL,
                accountsignaturehtml nvarchar(max) NOT NULL,
                accountlastlogontime datetime NOT NULL,
                accountpersonfirstname nvarchar(60) NOT NULL,
                accountpersonlastname nvarchar(60) NOT NULL
            );
            CREATE TABLE dbo.hm_messages (
                messageid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                messageaccountid int NOT NULL,
                messagesize bigint NOT NULL
            );
            CREATE TABLE dbo.hm_aliases (
                aliasid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                aliasdomainid int NOT NULL,
                aliasname nvarchar(255) NOT NULL,
                aliasvalue nvarchar(255) NOT NULL,
                aliasactive tinyint NOT NULL
            );
            CREATE TABLE dbo.hm_distributionlists (
                distributionlistid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                distributionlistdomainid int NOT NULL,
                distributionlistenabled tinyint NOT NULL,
                distributionlistaddress nvarchar(255) NOT NULL,
                distributionlistrequireauth tinyint NOT NULL,
                distributionlistrequireaddress nvarchar(255) NOT NULL,
                distributionlistmode tinyint NOT NULL
            );
            CREATE TABLE dbo.hm_distributionlistsrecipients (
                distributionlistrecipientid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                distributionlistrecipientlistid int NOT NULL,
                distributionlistrecipientaddress nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_domain_aliases (
                dadomainid int NOT NULL
            );
            CREATE TABLE dbo.hm_rules (
                ruleid int NOT NULL,
                ruleaccountid int NOT NULL
            );
            CREATE TABLE dbo.hm_rule_actions (
                actionruleid int NOT NULL
            );
            CREATE TABLE dbo.hm_rule_criterias (
                criteriaruleid int NOT NULL
            );
            CREATE TABLE dbo.hm_messagerecipients (
                recipientmessageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_metadata (
                metadata_accountid int NOT NULL
            );
            CREATE TABLE dbo.hm_message_search_queue (
                messageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_search_documents (
                messageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_fetchaccounts (
                faaccountid int NOT NULL
            );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
