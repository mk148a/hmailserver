using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using HMailServer.Service;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BackupRestoreRoundTripIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";
    private const string RetainArtifactsEnvironmentVariable = "HMAILSERVER_NET10_BACKUP_RETAIN_ARTIFACTS";
    private const string RetainArtifactsOutputEnvironmentVariable = "HMAILSERVER_NET10_BACKUP_RETAIN_OUTPUT";

    private static readonly string NonDbArchiveXml = $"""
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
                         AdminLevel="1" MaxAccountSize="128">
                      <Rules>
                        <Rule Name="subject rule" Active="1" UseAND="1" SortOrder="2">
                          <RuleCriterias>
                            <Criteria MatchString="needle" FieldType="1" MatchType="2"
                                      HeaderField="Subject" UsePredefinedField="1" />
                          </RuleCriterias>
                          <RuleActions>
                            <Action Type="1" Subject="changed" Body="body" FromAddress="from@example.test"
                                    FromName="From" IMAPFolder="INBOX.processed" FileName="file.eml"
                                    To="to@example.test" ScriptFunction="OnRule" SortOrder="3"
                                    Header="X-Test" Value="value" RouteID="4" AbortSpamFlagged="1" />
                          </RuleActions>
                        </Rule>
                      </Rules>
                      <Folders>
                        <Folder Name="INBOX" Subscribed="1" CreateTime="2026-07-01 12:30:00" CurrentUID="5">
                          <Messages>
                            <Message CreateTime="2026-07-01 12:32:00" Filename="one.eml" FromAddress="sender@example.test" State="2" Size="42" NoOfRetries="9" Flags="1" ID="77" UID="8" />
                          </Messages>
                          <Folders>
                            <Folder Name="child" Subscribed="0" CreateTime="2026-07-01 12:31:00" CurrentUID="2" />
                          </Folders>
                        </Folder>
                      </Folders>
                      <FetchAccounts>
                        <FetchAccount Name="fetcher" ServerAddress="pop3.example.test" ServerType="0"
                                      Port="995" Username="remote-user"
                                      Password="{LegacyBlowfishPasswordCipher.Encrypt("fetch-secret")}" Minutes="15"
                                      DaysToKeep="30" Active="1" MIMERecipientHeaders="To"
                                      ProcessMIMERecipients="1" ProcessMIMEDate="0" UseAntiSpam="1"
                                      UseAntiVirus="0" EnableRouteRecipients="1" ConnectionSecurity="1">
                          <FetchAccountUIDs>
                            <UID UID="uid-restore-1" Date="2026-07-01 12:30:00" />
                          </FetchAccountUIDs>
                        </FetchAccount>
                      </FetchAccounts>
                    </Account>
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

    private static readonly string FullRestoreArchiveXml = NonDbArchiveXml
        .Replace("Mode=\"6\"", "Mode=\"7\"", StringComparison.Ordinal)
        .Replace(
            "<Domains>",
            "<Properties><welcomesmtp StringValue=\"restored greeting\" LongValue=\"0\" /></Properties>\n          <Domains>",
            StringComparison.Ordinal);

    private static readonly string FullRestoreArchiveXmlWithTwoMessages = FullRestoreArchiveXml
        .Replace(
            "Filename=\"one.eml\" FromAddress=\"sender@example.test\" State=\"2\" Size=\"42\" NoOfRetries=\"9\" Flags=\"1\" ID=\"77\" UID=\"8\" />",
            "Filename=\"one.eml\" FromAddress=\"sender@example.test\" State=\"2\" Size=\"42\" NoOfRetries=\"9\" Flags=\"1\" ID=\"77\" UID=\"8\" />\n                            <Message CreateTime=\"2026-07-01 12:33:00\" Filename=\"two.eml\" FromAddress=\"sender2@example.test\" State=\"2\" Size=\"43\" NoOfRetries=\"4\" Flags=\"1\" ID=\"78\" UID=\"9\" />",
            StringComparison.Ordinal);

    private static readonly string FullRestoreArchiveXmlWithNonDeliveredMessage = FullRestoreArchiveXml
        .Replace(
            "Filename=\"one.eml\" FromAddress=\"sender@example.test\" State=\"2\" Size=\"42\" NoOfRetries=\"9\" Flags=\"1\" ID=\"77\" UID=\"8\" />",
            "Filename=\"one.eml\" FromAddress=\"sender@example.test\" State=\"1\" Size=\"42\" NoOfRetries=\"9\" Flags=\"1\" ID=\"77\" UID=\"8\" />",
            StringComparison.Ordinal);

    private static readonly string FullRestoreArchiveXmlWithPublicFolders = FullRestoreArchiveXml
        .Replace(
            "</Backup>",
            """
              <PublicFolders>
                <Folder Name="Shared" Subscribed="1" CreateTime="2026-07-01 13:00:00" CurrentUID="11">
                  <Messages>
                    <Message CreateTime="2026-07-01 13:01:00" Filename="public.eml" FromAddress="public@example.test"
                             State="2" Size="19" NoOfRetries="2" Flags="1" ID="91" UID="12" />
                  </Messages>
                  <Folders>
                    <Folder Name="Child" Subscribed="0" CreateTime="2026-07-01 13:02:00" CurrentUID="4" />
                  </Folders>
                  <ACLs>
                    <Permission Type="0" Rights="3" Holder="user@roundtrip.example" />
                  </ACLs>
                </Folder>
              </PublicFolders>
            </Backup>
            """,
            StringComparison.Ordinal);

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
                         AdminLevel="1" MaxAccountSize="128">
                  <Rules>
                    <Rule Name="subject rule" Active="1" UseAND="1" SortOrder="2">
                      <RuleCriterias>
                        <Criteria MatchString="needle" FieldType="1" MatchType="2"
                                  HeaderField="Subject" UsePredefinedField="1" />
                      </RuleCriterias>
                      <RuleActions>
                        <Action Type="1" Subject="changed" Body="body" FromAddress="from@example.test"
                                FromName="From" IMAPFolder="INBOX.processed" FileName="file.eml"
                                To="to@example.test" ScriptFunction="OnRule" SortOrder="3"
                                Header="X-Test" Value="value" RouteID="4" AbortSpamFlagged="1" />
                      </RuleActions>
                    </Rule>
                  </Rules>
                  <Folders>
                    <Folder Name="INBOX" Subscribed="1" CreateTime="2026-07-01 12:30:00" CurrentUID="5">
                      <Messages>
                        <Message CreateTime="2026-07-01 12:32:00" Filename="one.eml" FromAddress="sender@example.test" State="2" Size="42" NoOfRetries="9" Flags="1" ID="77" UID="8" />
                      </Messages>
                      <Folders>
                        <Folder Name="child" Subscribed="0" CreateTime="2026-07-01 12:31:00" CurrentUID="2" />
                      </Folders>
                    </Folder>
                  </Folders>
                </Account>
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
            var ruleStore = new SqlServerRuleAdministrationStore(factory);
            var criteriaStore = new SqlServerRuleCriteriaAdministrationStore(factory);
            var actionStore = new SqlServerRuleActionAdministrationStore(factory);
            await BackupRestoreMetadataWriter.RestoreRulesAsync(
                accounts.Single().Rules,
                accountId: 1,
                ruleStore,
                criteriaStore,
                actionStore,
                rollback,
                CancellationToken.None).ConfigureAwait(false);
            var folderStore = new SqlServerImapFolderAdministrationStore(factory);
            await BackupRestoreMetadataWriter.RestoreFoldersAsync(
                accounts.Single().Folders,
                accountId: 1,
                folderStore,
                new SqlServerMessageAdministrationStore(factory),
                rollback,
                CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreAliasesAsync(aliases, domainId: 1, aliasStore, rollback, CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreDistributionListsAsync(lists, domainId: 1, listStore, rollback, CancellationToken.None).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreDistributionListRecipientsAsync(recipients, distributionListId: 1, recipientStore, rollback, CancellationToken.None).ConfigureAwait(false);

            var restoredDomain = (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("roundtrip.example", restoredDomain.Name);
            Assert.IsTrue(restoredDomain.Active);

            var restoredAccount = (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("user@roundtrip.example", restoredAccount.Address);
            Assert.AreEqual(128, restoredAccount.MaxSize);

            var restoredRule = (await ruleStore.GetRulesAsync(1, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("subject rule", restoredRule.Name);
            var restoredCriteria = (await criteriaStore.GetRuleCriteriaAsync(restoredRule.Id, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("needle", restoredCriteria.MatchValue);
            var restoredAction = (await actionStore.GetRuleActionsAsync(restoredRule.Id, CancellationToken.None).ConfigureAwait(false)).Single();
            Assert.AreEqual("to@example.test", restoredAction.To);
            Assert.AreEqual(4, restoredAction.RouteId);

            var restoredFolders = await folderStore.GetFoldersForAccountAsync(1, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(2, restoredFolders.Count);
            Assert.AreEqual(5, restoredFolders.Single(folder => folder.Name == "INBOX").CurrentUid);
            Assert.AreEqual(1, restoredFolders.Single(folder => folder.Name == "child").ParentId);
            var restoredMessages = await new SqlServerMessageAdministrationStore(factory)
                .GetFolderMessagesAsync(1, restoredFolders.Single(folder => folder.Name == "INBOX").Id, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.AreEqual(1, restoredMessages.Count);
            Assert.AreEqual("one.eml", restoredMessages[0].FileName);
            Assert.AreEqual(8, restoredMessages[0].Uid);
            Assert.AreEqual(0, restoredMessages[0].CurrentNumberOfTries);
            Assert.AreEqual(33, restoredMessages[0].Flags);

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
            var messagePath = Path.Combine(dataBackup, "roundtrip.example", "user", "ne");
            Directory.CreateDirectory(messagePath);
            File.WriteAllText(Path.Combine(messagePath, "one.eml"), "From: sender@example.test\r\n\r\nbody");
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
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                fetchAccountStore: new SqlServerFetchAccountAdministrationStore(factory),
                ruleStore: new SqlServerRuleAdministrationStore(factory),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(factory),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(factory),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(factory),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(factory),
                messageRestoreStore: new SqlServerMessageAdministrationStore(factory),
                messageStore: new SqlServerMessageAdministrationStore(factory));
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
            var restoredFolders = await new SqlServerImapFolderAdministrationStore(factory)
                .GetFoldersForAccountAsync(1, CancellationToken.None)
                .ConfigureAwait(false);
            var restoredMessages = await new SqlServerMessageAdministrationStore(factory)
                .GetFolderMessagesAsync(1, restoredFolders.Single(folder => folder.Name == "INBOX").Id, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.AreEqual(1, restoredMessages.Count);
            Assert.AreEqual(8, restoredMessages[0].Uid);
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

    private static bool TryRetainTestRoot(string root, string testName)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(RetainArtifactsEnvironmentVariable),
                "1",
                StringComparison.Ordinal)
            || !Directory.Exists(root))
        {
            return false;
        }

        var output = Environment.GetEnvironmentVariable(RetainArtifactsOutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                $"{RetainArtifactsOutputEnvironmentVariable} is required when {RetainArtifactsEnvironmentVariable}=1.");
        }

        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var retainedRoot = Path.Combine(output, $"{testName}-{Guid.NewGuid():N}");
        CopyDirectory(root, retainedRoot);
        Directory.Delete(root, recursive: true);
        return true;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(destination, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RestoresFullSettingsDomainsAndMessagesInOneTransaction()
    {
        await WithFullRestoreTargetAsync(
            "full_restore",
            FullRestoreArchiveXml,
            async fixture =>
            {
                var staleGroupId = await SeedGroupGraphAsync(fixture.ConnectionString, "Stale Editors").ConfigureAwait(false);
                await fixture.CreateExecutor().ExecuteAsync(fixture.Backup, CancellationToken.None).ConfigureAwait(false);

                Assert.AreEqual(
                    "restored greeting",
                    await ReadSettingStringAsync(fixture.ConnectionString, "welcomesmtp").ConfigureAwait(false));
                Assert.AreEqual(1, (await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(1, (await fixture.AccountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(2, await CountRowsAsync(fixture.ConnectionString, "hm_imapfolders", "folderaccountid", 1).ConfigureAwait(false));
                Assert.AreEqual(1, await CountRowsAsync(fixture.ConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
                Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_groups", "groupid", staleGroupId).ConfigureAwait(false));
                Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_acl", "aclpermissiongroupid", staleGroupId).ConfigureAwait(false));
                Assert.AreEqual(1, await CountRowsAsync(fixture.ConnectionString, "hm_group_members", "membergroupid", staleGroupId).ConfigureAwait(false));
                Assert.AreEqual("restored", await File.ReadAllTextAsync(Path.Combine(fixture.GetDataDirectory(), "restored.txt")));
                Assert.IsFalse(File.Exists(Path.Combine(fixture.GetDataDirectory(), "original.txt")));
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreGroups_CommitReplacesGroupsAndOwnedAclInIsolatedDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_groups_commit_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var oldGroupId = await SeedGroupGraphAsync(testConnectionString, "Old Editors").ConfigureAwait(false);
            var newGroupId = 0;

            await using (var transaction = await new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString)).BeginAsync(CancellationToken.None))
            {
                await transaction.DeleteAllGroupsForRestoreAsync(CancellationToken.None).ConfigureAwait(false);
                newGroupId = await transaction.GroupStore!.InsertGroupAsync(
                    new GroupAdministrationSnapshot(0, "Editors"),
                    CancellationToken.None).ConfigureAwait(false);
                await transaction.GroupMemberStore!.InsertGroupMemberAsync(
                    new GroupMemberAdministrationSnapshot(0, newGroupId, 42),
                    CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_groups", "groupid", oldGroupId).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_acl", "aclpermissiongroupid", oldGroupId).ConfigureAwait(false));
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_groups", "groupid", newGroupId).ConfigureAwait(false));
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_group_members", "membergroupid", newGroupId).ConfigureAwait(false));
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_group_members", "membergroupid", oldGroupId).ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreGroups_DisposalRollsBackReplacementAndOwnedAclInIsolatedDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_groups_rollback_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var oldGroupId = await SeedGroupGraphAsync(testConnectionString, "Old Editors").ConfigureAwait(false);

            await using (var transaction = await new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString)).BeginAsync(CancellationToken.None))
            {
                await transaction.DeleteAllGroupsForRestoreAsync(CancellationToken.None).ConfigureAwait(false);
                var newGroupId = await transaction.GroupStore!.InsertGroupAsync(
                    new GroupAdministrationSnapshot(0, "Editors"),
                    CancellationToken.None).ConfigureAwait(false);
                await transaction.GroupMemberStore!.InsertGroupMemberAsync(
                    new GroupMemberAdministrationSnapshot(0, newGroupId, 42),
                    CancellationToken.None).ConfigureAwait(false);
            }

            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_groups", "groupid", oldGroupId).ConfigureAwait(false));
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_acl", "aclpermissiongroupid", oldGroupId).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsByNameAsync(testConnectionString, "hm_groups", "groupname", "Editors").ConfigureAwait(false));
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_group_members", "membergroupid", oldGroupId).ConfigureAwait(false));
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_groups").ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RestoresPopulatedPublicFoldersAndMessagesIntoDisposableTarget()
    {
        await WithFullRestoreTargetAsync(
            "full_public_restore",
            FullRestoreArchiveXmlWithPublicFolders,
            async fixture =>
            {
                await fixture.CreateExecutor().ExecuteAsync(fixture.Backup, CancellationToken.None).ConfigureAwait(false);

                var folderStore = new SqlServerImapFolderAdministrationStore(
                    new SqlServerConnectionFactory(fixture.ConnectionString));
                var messageStore = new SqlServerMessageAdministrationStore(
                    new SqlServerConnectionFactory(fixture.ConnectionString));
                var folders = await folderStore.GetFoldersForAccountAsync(0, CancellationToken.None).ConfigureAwait(false);

                Assert.AreEqual(2, folders.Count);
                Assert.AreEqual("Shared", folders.Single(folder => folder.ParentId == -1).Name);
                Assert.AreEqual("Child", folders.Single(folder => folder.ParentId > 0).Name);
                Assert.AreEqual(11, folders.Single(folder => folder.Name == "Shared").CurrentUid);

                var messages = await messageStore.GetFolderMessagesAsync(
                    0,
                    folders.Single(folder => folder.Name == "Shared").Id,
                    CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(1, messages.Count);
                Assert.AreEqual("public.eml", messages[0].FileName);
                Assert.AreEqual(12, messages[0].Uid);
                Assert.AreEqual(0, messages[0].CurrentNumberOfTries);
                Assert.AreEqual(33, messages[0].Flags);
                Assert.AreEqual(2, await CountRowsAsync(fixture.ConnectionString, "hm_imapfolders", "folderaccountid", 0).ConfigureAwait(false));
                Assert.AreEqual(1, await CountRowsAsync(fixture.ConnectionString, "hm_messages", "messageaccountid", 0).ConfigureAwait(false));
                Assert.AreEqual(1, await CountRowsAsync(fixture.ConnectionString, "hm_acl", "aclpermissionaccountid", 1).ConfigureAwait(false));
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task BackupManager_StartRestoreDispatchesRealFullRestoreIntoPopulatedTarget()
    {
        await WithFullRestoreTargetAsync(
            "manager_full_restore",
            FullRestoreArchiveXml,
            async fixture =>
            {
                await fixture.SeedExistingDomainAndPublicFolderAsync().ConfigureAwait(false);

                using var queue = new BackupTaskQueue();
                var readiness = new ServerReadinessSignal();
                readiness.SetBootstrapComplete();
                using var service = new BackupTaskHostedService(
                    queue,
                    NullLogger<BackupTaskHostedService>.Instance,
                    readiness);
                var dispatcher = new RecordingBackupEventDispatcher();
                var manager = BackupManager.CreateAuthorized(
                    new SevenZipBackupArchiveMetadataReader(Path.Combine(AppContext.BaseDirectory, "7za.exe")),
                    new BackupOperationRuntime(queue),
                    eventDispatcher: dispatcher,
                    restoreExecutor: fixture.CreateExecutor());

                var backup = (Backup)manager.LoadBackup(fixture.ArchivePath);
                try
                {
                    backup.RestoreSettings = true;
                    backup.RestoreDomains = true;
                    backup.RestoreMessages = true;

                    await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    backup.StartRestore();

                    var completed = await Task.WhenAny(
                        dispatcher.Completed.Task,
                        dispatcher.Failed.Task).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    if (ReferenceEquals(completed, dispatcher.Failed.Task))
                    {
                        Assert.Fail("The real queued restore failed: " + dispatcher.Failed.Task.Result);
                    }

                    Assert.AreEqual(
                        "restored greeting",
                        await ReadSettingStringAsync(fixture.ConnectionString, "welcomesmtp").ConfigureAwait(false));
                    Assert.AreEqual(1, (await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
                    Assert.AreEqual(
                        0,
                        await CountRowsAsync(fixture.ConnectionString, "hm_imapfolders", "folderaccountid", 0).ConfigureAwait(false));
                    Assert.AreEqual(2, await CountRowsAsync(fixture.ConnectionString, "hm_imapfolders", "folderaccountid", 1).ConfigureAwait(false));
                    Assert.AreEqual(1, await CountRowsAsync(fixture.ConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
                    Assert.AreEqual("restored", await File.ReadAllTextAsync(Path.Combine(fixture.GetDataDirectory(), "restored.txt")).ConfigureAwait(false));
                    Assert.IsFalse(File.Exists(Path.Combine(fixture.GetDataDirectory(), "original.txt")));
                }
                finally
                {
                    backup.CleanupArchiveBinding();
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task BackupManager_StartBackupLoadBackupAndRestoreRoundTripsRealArchive()
    {
        await WithFullRestoreTargetAsync(
            "manager_backup_restore",
            FullRestoreArchiveXml,
            async fixture =>
            {
                var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
                var destination = Path.Combine(
                    Directory.GetParent(fixture.GetDataDirectory())!.FullName,
                    "generated-backup");
                Directory.CreateDirectory(destination);
                var generatedMessagePath = Path.Combine(
                    fixture.GetDataDirectory(),
                    "roundtrip.example",
                    "user",
                    "ne");
                Directory.CreateDirectory(generatedMessagePath);
                await File.WriteAllTextAsync(
                    Path.Combine(generatedMessagePath, "generated.eml"),
                    "From: generated@example.test\r\n\r\ncreated by StartBackup")
                    .ConfigureAwait(false);

                using var queue = new BackupTaskQueue();
                var readiness = new ServerReadinessSignal();
                readiness.SetBootstrapComplete();
                using var service = new BackupTaskHostedService(
                    queue,
                    NullLogger<BackupTaskHostedService>.Instance,
                    readiness);
                var archivePath = Path.Combine(destination, "HMBackup 2026-08-11 040507.7z");
                var dispatcher = new RecordingBackupEventDispatcher(
                    completedProbe: () => File.Exists(archivePath));
                var evidence = new BackupStartPlanEvidence(
                    Destination: destination,
                    BackupOptions: 1 | 2 | 4,
                    BackupMessagesDbOnly: false,
                    AllMessageFilesInDataDirectory: true,
                    DestinationExists: true,
                    Settings: new SettingsAdministrationSnapshot(
                        "generated.example",
                        "generated smtp",
                        "generated pop3",
                        "generated imap"));
                var archiveRuntime = new SevenZipBackupArchiveRuntime(
                    sevenZipPath,
                    "10.0.0-B0",
                    static () => new DateTime(2026, 8, 11, 4, 5, 7),
                    payloadProvider: (startEvidence, _) => ValueTask.FromResult(
                        new BackupArchiveXmlPayload(
                            startEvidence.Settings,
                            new[]
                            {
                                new DomainAdministrationSnapshot(
                                    0,
                                    "generated.example",
                                    true,
                                    Postmaster: "postmaster@generated.example")
                            })),
                    dataDirectory: fixture.GetDataDirectory());
                var operationRuntime = new BackupOperationRuntime(
                    queue,
                    startPlanEvidence: _ => ValueTask.FromResult(evidence),
                    executeBackupAsync: (startEvidence, cancellationToken) =>
                        archiveRuntime.CreateAsync(startEvidence, cancellationToken));
                var manager = BackupManager.CreateAuthorized(
                    new SevenZipBackupArchiveMetadataReader(sevenZipPath),
                    operationRuntime,
                    eventDispatcher: dispatcher,
                    restoreExecutor: fixture.CreateExecutor());

                await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                manager.StartBackup();
                await dispatcher.Completed.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);

                Assert.IsTrue(File.Exists(archivePath), archivePath);
                Assert.IsTrue(dispatcher.CompletedArchiveExistsAtDispatch);
                Assert.IsTrue(File.Exists(Path.Combine(destination, "DataBackup", "roundtrip.example", "user", "ne", "generated.eml")));

                var backup = (Backup)manager.LoadBackup(archivePath);
                try
                {
                    Assert.IsTrue(backup.ContainsSettings);
                    Assert.IsTrue(backup.ContainsDomains);
                    Assert.IsTrue(backup.ContainsMessages);
                    backup.RestoreSettings = true;
                    backup.RestoreDomains = true;
                    backup.RestoreMessages = true;
                    backup.StartRestore();
                    var restoreCompletion = await Task.WhenAny(
                        dispatcher.SecondCompleted.Task,
                        dispatcher.Failed.Task).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    if (ReferenceEquals(restoreCompletion, dispatcher.Failed.Task))
                    {
                        Assert.Fail("The real StartBackup-to-restore flow failed: " + dispatcher.Failed.Task.Result);
                    }

                    Assert.AreEqual(
                        "generated smtp",
                        await ReadSettingStringAsync(fixture.ConnectionString, "welcomesmtp").ConfigureAwait(false));
                    var domains = await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false);
                    Assert.AreEqual(1, domains.Count);
                    Assert.AreEqual("generated.example", domains[0].Name);
                    Assert.IsTrue(File.Exists(Path.Combine(
                        fixture.GetDataDirectory(),
                        "roundtrip.example",
                        "user",
                        "ne",
                        "generated.eml")));
                    Assert.IsFalse(File.Exists(Path.Combine(fixture.GetDataDirectory(), "original.txt")));
                }
                finally
                {
                    backup.CleanupArchiveBinding();
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task BackupManager_StartBackupRawNonDbOnlyMode2And4PublishesDataBackupSibling()
    {
        await WithFullRestoreTargetAsync(
            "manager_backup_raw_non_db_only_2_4",
            FullRestoreArchiveXmlWithNonDeliveredMessage,
            async fixture =>
            {
                var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
                var destination = Path.Combine(
                    Directory.GetParent(fixture.GetDataDirectory())!.FullName,
                    "generated-raw-backup");
                Directory.CreateDirectory(destination);
                var generatedMessagePath = Path.Combine(
                    fixture.GetDataDirectory(),
                    "roundtrip.example",
                    "user",
                    "ne");
                Directory.CreateDirectory(generatedMessagePath);
                await File.WriteAllTextAsync(
                    Path.Combine(generatedMessagePath, "generated.eml"),
                    "From: generated@example.test\r\n\r\ncreated by raw StartBackup")
                    .ConfigureAwait(false);

                using var queue = new BackupTaskQueue();
                var readiness = new ServerReadinessSignal();
                readiness.SetBootstrapComplete();
                using var service = new BackupTaskHostedService(
                    queue,
                    NullLogger<BackupTaskHostedService>.Instance,
                    readiness);
                var dispatcher = new RecordingBackupEventDispatcher();
                var evidence = new BackupStartPlanEvidence(
                    Destination: destination,
                    BackupOptions: 2 | 4,
                    BackupMessagesDbOnly: false,
                    AllMessageFilesInDataDirectory: true,
                    DestinationExists: true,
                    Settings: new SettingsAdministrationSnapshot(
                        "generated.example",
                        "generated smtp",
                        "generated pop3",
                        "generated imap"));
                var archiveRuntime = new SevenZipBackupArchiveRuntime(
                    sevenZipPath,
                    "10.0.0-B0",
                    static () => new DateTime(2026, 8, 21, 4, 5, 7),
                    payloadProvider: (startEvidence, _) => ValueTask.FromResult(
                        new BackupArchiveXmlPayload(
                            startEvidence.Settings,
                            new[]
                            {
                                new DomainAdministrationSnapshot(
                                    0,
                                    "generated.example",
                                    true,
                                    Postmaster: "postmaster@generated.example")
                            })),
                    dataDirectory: fixture.GetDataDirectory());
                var operationRuntime = new BackupOperationRuntime(
                    queue,
                    startPlanEvidence: _ => ValueTask.FromResult(evidence),
                    executeBackupAsync: (startEvidence, cancellationToken) =>
                        archiveRuntime.CreateAsync(startEvidence, cancellationToken));
                var manager = BackupManager.CreateAuthorized(
                    new SevenZipBackupArchiveMetadataReader(sevenZipPath),
                    operationRuntime,
                    eventDispatcher: dispatcher,
                    restoreExecutor: fixture.CreateExecutor());

                await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    manager.StartBackup();
                    await WaitForBackupCompletionAsync(dispatcher.Completed.Task, dispatcher.Failed.Task)
                        .ConfigureAwait(false);

                    var archivePath = Path.Combine(destination, "HMBackup 2026-08-21 040507.7z");
                    var rawMessagePath = Path.Combine(
                        destination,
                        "DataBackup",
                        "roundtrip.example",
                        "user",
                        "ne",
                        "generated.eml");
                    Assert.IsTrue(File.Exists(archivePath), archivePath);
                    Assert.IsTrue(File.Exists(rawMessagePath), rawMessagePath);
                    Assert.AreEqual(
                        "From: generated@example.test\r\n\r\ncreated by raw StartBackup",
                        await File.ReadAllTextAsync(rawMessagePath).ConfigureAwait(false));

                    var metadata = XDocument.Parse(
                        new SevenZipBackupArchiveMetadataReader(sevenZipPath)
                            .ReadMetadataXml(archivePath));
                    var dataFiles = metadata
                        .Element("Backup")?
                        .Element("BackupInformation")?
                        .Element("DataFiles");
                    Assert.AreEqual("Raw", dataFiles?.Attribute("Format")?.Value);
                    Assert.AreEqual("DataBackup", dataFiles?.Attribute("FolderName")?.Value);
                    Assert.AreEqual(
                        "6",
                        metadata.Element("Backup")?.Element("BackupInformation")?.Attribute("Mode")?.Value);
                }
                finally
                {
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task BackupManager_BackupRestoreBackupPreservesNonDeliveredMessageStateAndDataSemantics()
    {
        await WithFullRestoreTargetAsync(
            "manager_backup_restore_backup_semantics",
            FullRestoreArchiveXmlWithNonDeliveredMessage,
            async fixture =>
            {
                await fixture.CreateExecutor().ExecuteAsync(fixture.Backup, CancellationToken.None).ConfigureAwait(false);

                var factory = new SqlServerConnectionFactory(fixture.ConnectionString);
                var settingsStore = new SqlServerSettingsAdministrationStore(factory);
                var domainAliasStore = new SqlServerDomainAliasAdministrationStore(factory);
                var fetchAccountStore = new SqlServerFetchAccountAdministrationStore(factory);
                var backupFetchAccountStore = new SqlServerBackupFetchAccountAdministrationStore(factory);
                var ruleStore = new SqlServerRuleAdministrationStore(factory);
                var criteriaStore = new SqlServerRuleCriteriaAdministrationStore(factory);
                var actionStore = new SqlServerRuleActionAdministrationStore(factory);
                var folderStore = new SqlServerImapFolderAdministrationStore(factory);
                var messageStore = new SqlServerMessageAdministrationStore(factory);
                var inbox = (await folderStore
                    .GetFoldersForAccountAsync(1, CancellationToken.None)
                    .ConfigureAwait(false))
                    .Single(folder => folder.Name == "INBOX");
                var backupMessages = await messageStore
                    .GetFolderMessagesForBackupAsync(1, inbox.Id, CancellationToken.None)
                    .ConfigureAwait(false);
                Assert.AreEqual(1, backupMessages.Count);
                Assert.AreEqual(1, backupMessages[0].State);
                Assert.AreEqual(0, backupMessages[0].CurrentNumberOfTries);
                Assert.AreEqual(33, backupMessages[0].Flags);
                Assert.IsTrue(File.Exists(Path.Combine(
                    fixture.GetDataDirectory(),
                    "roundtrip.example",
                    "user",
                    "ne",
                    "one.eml")));
                var sharedMessages = await messageStore
                    .GetFolderMessagesAsync(1, inbox.Id, CancellationToken.None)
                    .ConfigureAwait(false);
                Assert.AreEqual(0, sharedMessages.Count);
                var payloadRuntime = new BackupXmlPayloadRuntime(
                    settingsStore,
                    fixture.DomainStore,
                    domainAliasStore,
                    fixture.AccountStore,
                    fixture.AliasStore,
                    fixture.ListStore,
                    fixture.RecipientStore,
                    backupAccountStore: fixture.AccountStore,
                    fetchAccountStore: fetchAccountStore,
                    backupFetchAccountStore: backupFetchAccountStore,
                    backupRuleStore: ruleStore,
                    ruleStore: ruleStore,
                    ruleCriteriaStore: criteriaStore,
                    ruleActionStore: actionStore,
                    folderStore: folderStore,
                    folderRestoreStore: folderStore,
                    messageStore: messageStore);
                var firstDestination = Path.Combine(Directory.GetParent(fixture.GetDataDirectory())!.FullName, "backup-one");
                var secondDestination = Path.Combine(Directory.GetParent(fixture.GetDataDirectory())!.FullName, "backup-two");
                Directory.CreateDirectory(firstDestination);
                Directory.CreateDirectory(secondDestination);
                var currentDestination = firstDestination;
                var evidence = new BackupStartPlanEvidence(
                    currentDestination,
                    BackupOptions: 1 | 2 | 4,
                    BackupMessagesDbOnly: false,
                    AllMessageFilesInDataDirectory: true,
                    DestinationExists: true,
                    Settings: new SettingsAdministrationSnapshot(
                        "roundtrip.example",
                        "restored smtp",
                        "restored pop3",
                        "restored imap"));
                var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
                var archiveRuntime = new SevenZipBackupArchiveRuntime(
                    sevenZipPath,
                    "10.0.0-B0",
                    localNow: static () => new DateTime(2026, 8, 14, 4, 5, 7),
                    payloadProvider: payloadRuntime.GetPayloadAsync,
                    dataDirectory: fixture.GetDataDirectory());

                using var queue = new BackupTaskQueue();
                var readiness = new ServerReadinessSignal();
                readiness.SetBootstrapComplete();
                using var service = new BackupTaskHostedService(
                    queue,
                    NullLogger<BackupTaskHostedService>.Instance,
                    readiness);
                var dispatcher = new RecordingBackupEventDispatcher();
                var operationRuntime = new BackupOperationRuntime(
                    queue,
                    startPlanEvidence: _ => ValueTask.FromResult(evidence with { Destination = currentDestination }),
                    executeBackupAsync: (startEvidence, cancellationToken) =>
                        archiveRuntime.CreateAsync(startEvidence, cancellationToken));
                var manager = BackupManager.CreateAuthorized(
                    new SevenZipBackupArchiveMetadataReader(sevenZipPath),
                    operationRuntime,
                    eventDispatcher: dispatcher,
                    restoreExecutor: fixture.CreateExecutor());

                await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    manager.StartBackup();
                    await WaitForBackupCompletionAsync(dispatcher.Completed.Task, dispatcher.Failed.Task).ConfigureAwait(false);
                    var firstArchive = Directory.GetFiles(firstDestination, "HMBackup *.7z").Single();
                    var metadataReader = new SevenZipBackupArchiveMetadataReader(sevenZipPath);
                    var backup = (Backup)manager.LoadBackup(firstArchive);
                    try
                    {
                        backup.RestoreSettings = true;
                        backup.RestoreDomains = true;
                        backup.RestoreMessages = true;
                        backup.StartRestore();
                        await WaitForBackupCompletionAsync(dispatcher.SecondCompleted.Task, dispatcher.Failed.Task).ConfigureAwait(false);

                        currentDestination = secondDestination;
                        manager.StartBackup();
                        await WaitForBackupCompletionAsync(dispatcher.ThirdCompleted.Task, dispatcher.Failed.Task).ConfigureAwait(false);
                    }
                    finally
                    {
                        backup.CleanupArchiveBinding();
                    }

                    var secondArchive = Directory.GetFiles(secondDestination, "HMBackup *.7z").Single();
                    Assert.AreEqual(
                        NormalizeBackupXml(metadataReader.ReadMetadataXml(firstArchive)),
                        NormalizeBackupXml(metadataReader.ReadMetadataXml(secondArchive)));

                    var firstDataBackup = Path.Combine(firstDestination, "DataBackup");
                    var secondDataBackup = Path.Combine(secondDestination, "DataBackup");
                    CollectionAssert.AreEqual(
                        GetDataBackupEvidence(firstDataBackup),
                        GetDataBackupEvidence(secondDataBackup));
                    Assert.IsTrue(GetDataBackupEvidence(firstDataBackup).Length > 0);
                }
                finally
                {
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_RollsBackFullSettingsMetadataAndDataWhenSecondMessageInsertFails()
    {
        await WithFullRestoreTargetAsync(
            "full_restore_message_failure",
            FullRestoreArchiveXmlWithTwoMessages,
            async fixture =>
            {
                var executor = fixture.CreateExecutor(
                    new FailingMetadataTransactionFactory(fixture.TransactionFactory, failSecondMessage: true),
                    filesystemMutation: new DeterministicFilesystemMutation());

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => executor.ExecuteAsync(fixture.Backup, CancellationToken.None).AsTask()).ConfigureAwait(false);

                Assert.AreEqual("old greeting", await ReadSettingStringAsync(fixture.ConnectionString, "welcomesmtp").ConfigureAwait(false));
                Assert.AreEqual(0, (await fixture.DomainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
                Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_imapfolders", "folderaccountid", 1).ConfigureAwait(false));
                Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
                Assert.AreEqual("original", await File.ReadAllTextAsync(Path.Combine(fixture.GetDataDirectory(), "original.txt")));
                Assert.IsFalse(File.Exists(Path.Combine(fixture.GetDataDirectory(), "restored.txt")));
                Assert.IsFalse(Directory.Exists(Path.Combine(fixture.GetDataDirectory(), "roundtrip.example")));
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreExecutor_PreservesJournalWhenFullRestoreCommitOutcomeIsAmbiguous()
    {
        await WithFullRestoreTargetAsync(
            "full_restore_ambiguous_commit",
            FullRestoreArchiveXml,
            async fixture =>
            {
                var executor = fixture.CreateExecutor(
                    new FailingMetadataTransactionFactory(
                        fixture.TransactionFactory,
                        throwAfterCommit: true),
                    filesystemMutation: new DeterministicFilesystemMutation());

                var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => executor.ExecuteAsync(fixture.Backup, CancellationToken.None).AsTask())
                    .ConfigureAwait(false);
                StringAssert.Contains(exception.Message, "ambiguous");

                var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(
                    fixture.GetDataDirectory());
                Assert.IsTrue(pending.IsPending);
                Assert.IsTrue(pending.RequiresManualRecovery);
                Assert.AreEqual(
                    BackupRestoreRecoveryPhase.MetadataCommitStarted,
                    pending.Manifest!.Phase);
                Assert.IsTrue(File.Exists(Path.Combine(fixture.GetDataDirectory(), "restored.txt")));
                Assert.IsTrue(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.GetDataDirectory())));
                Assert.IsTrue(Directory.Exists(pending.Manifest.RollbackPath));

                var restartGateException = Assert.ThrowsExactly<InvalidOperationException>(
                    () => BackupRestoreRecoveryJournal.EnsureNoPendingRecovery(
                        fixture.GetDataDirectory()));
                StringAssert.Contains(restartGateException.Message, "manual recovery");
                Assert.IsTrue(File.Exists(Path.Combine(fixture.GetDataDirectory(), "restored.txt")));
            }).ConfigureAwait(false);
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
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                fetchAccountStore: new SqlServerFetchAccountAdministrationStore(factory),
                ruleStore: new SqlServerRuleAdministrationStore(factory),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(factory),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(factory),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(factory),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(factory),
                messageRestoreStore: new SqlServerMessageAdministrationStore(factory),
                messageStore: new SqlServerMessageAdministrationStore(factory));
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
    public async Task RestoreExecutor_RollsBackRootFolderWhenMessageInsertFailsAfterDataStaging()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_executor_message_failure_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-executor-message-failure-{Guid.NewGuid():N}");
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
            var messagePath = Path.Combine(dataBackup, "roundtrip.example", "user", "ne");
            Directory.CreateDirectory(messagePath);
            File.WriteAllText(Path.Combine(messagePath, "one.eml"), "From: sender@example.test\r\n\r\nbody");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), NonDbArchiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            var domainStore = new SqlServerDomainAdministrationStore(factory);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                domainStore,
                accountStore,
                new SqlServerAliasAdministrationStore(factory),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                dataDirectoryRuntime: new BackupRestoreDataDirectoryRuntime(
                    Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                    filesystemMutation: new DeterministicFilesystemMutation()),
                dataDirectoryBoundaryFactory: () => new BackupRestoreDataDirectoryBoundary(
                    dataDirectory,
                    Path.Combine(root, "restore.rollback")),
                fetchAccountStore: new SqlServerFetchAccountAdministrationStore(factory),
                ruleStore: new SqlServerRuleAdministrationStore(factory),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(factory),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(factory),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(factory),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(factory),
                messageRestoreStore: new FailingMessageAdministrationRestoreStore(),
                messageStore: new SqlServerMessageAdministrationStore(factory));
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
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "roundtrip.example", "user", "ne", "one.eml")));
            Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(dataDirectory)));
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(root, "*.rollback").Any());
            Assert.AreEqual(0, (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_imapfolders", "folderaccountid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
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
    public async Task RestoreExecutor_RollsBackPreviouslyInsertedMessageWhenSecondMessageInsertFails()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_executor_second_message_failure_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var root = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-executor-second-message-failure-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateTargetSchemaAsync(testConnectionString).ConfigureAwait(false);
            var source = Path.Combine(root, "source");
            var dataDirectory = Path.Combine(root, "data");
            var dataBackup = Path.Combine(root, "DataBackup");
            var archivePath = Path.Combine(root, "backup.7z");
            var firstMessage = "<Message CreateTime=\"2026-07-01 12:32:00\" Filename=\"one.eml\" FromAddress=\"sender@example.test\" State=\"2\" Size=\"42\" NoOfRetries=\"9\" Flags=\"1\" ID=\"77\" UID=\"8\" />";
            var archiveXml = NonDbArchiveXml.Replace(
                firstMessage,
                firstMessage + "\n                            <Message CreateTime=\"2026-07-01 12:33:00\" Filename=\"two.eml\" FromAddress=\"sender2@example.test\" State=\"2\" Size=\"43\" NoOfRetries=\"4\" Flags=\"1\" ID=\"78\" UID=\"9\" />",
                StringComparison.Ordinal);
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            var messagePath = Path.Combine(dataBackup, "roundtrip.example", "user", "ne");
            Directory.CreateDirectory(messagePath);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(messagePath, "one.eml"), "From: sender@example.test\r\n\r\nbody one");
            File.WriteAllText(Path.Combine(messagePath, "two.eml"), "From: sender2@example.test\r\n\r\nbody two");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), archiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            var domainStore = new SqlServerDomainAdministrationStore(factory);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                domainStore,
                accountStore,
                new SqlServerAliasAdministrationStore(factory),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                dataDirectoryBoundaryFactory: () => new BackupRestoreDataDirectoryBoundary(
                    dataDirectory,
                    Path.Combine(root, "restore.rollback")),
                fetchAccountStore: new SqlServerFetchAccountAdministrationStore(factory),
                ruleStore: new SqlServerRuleAdministrationStore(factory),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(factory),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(factory),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(factory),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(factory),
                messageRestoreStore: new FailingOnSecondMessageAdministrationRestoreStore(
                    new SqlServerMessageAdministrationStore(factory)),
                messageStore: new SqlServerMessageAdministrationStore(factory));
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
            Assert.IsFalse(Directory.Exists(Path.Combine(dataDirectory, "roundtrip.example")));
            Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(dataDirectory)));
            Assert.IsFalse(File.Exists(Path.Combine(root, "restore.rollback")));
            Assert.AreEqual(0, (await domainStore.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, (await accountStore.GetAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_imapfolders", "folderaccountid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
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
                    new SqlServerDistributionListRecipientAdministrationStore(factory)),
                fetchAccountStore: new SqlServerFetchAccountAdministrationStore(factory),
                ruleStore: new SqlServerRuleAdministrationStore(factory),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(factory),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(factory),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(factory),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(factory),
                messageRestoreStore: new SqlServerMessageAdministrationStore(factory),
                messageStore: new SqlServerMessageAdministrationStore(factory));
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
                new FailingOnSecondRecipientAdministrationStore(recipientStore),
                fetchAccountStore: new SqlServerFetchAccountAdministrationStore(factory),
                ruleStore: new SqlServerRuleAdministrationStore(factory),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(factory),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(factory),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(factory),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(factory),
                messageRestoreStore: new SqlServerMessageAdministrationStore(factory),
                messageStore: new SqlServerMessageAdministrationStore(factory));
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
                var fetchAccounts = await fixture.FetchAccountStore.GetFetchAccountsAsync(1, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(1, fetchAccounts.Count);
                Assert.AreEqual("fetcher", fetchAccounts[0].Name);
                Assert.AreEqual(1, await CountRowsAsync(fixture.ConnectionString, "hm_fetchaccounts_uids", "uidfaid", fetchAccounts[0].Id).ConfigureAwait(false));
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
    public async Task RestoreExecutor_RollsBackFetchAccountWhenUidDateIsInvalid()
    {
        var archiveXml = ToDbOnlyArchiveXml(
            NonDbArchiveXml.Replace(
                "2026-07-01 12:30:00",
                "not-a-legacy-date",
                StringComparison.Ordinal));
        await WithDbOnlyRestoreTargetAsync(
            "db_only_fetch_uid_failure",
            archiveXml,
            async fixture =>
            {
                var executor = fixture.CreateExecutor();

                await Assert.ThrowsExactlyAsync<FormatException>(
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
    public async Task RestoreExecutor_RollsBackDbOnlyMetadataWhenRuleActionInsertFails()
    {
        await WithDbOnlyRestoreTargetAsync(
            "db_only_rule_action_failure",
            ToDbOnlyArchiveXml(NonDbArchiveXml),
            async fixture =>
            {
                var executor = fixture.CreateExecutor(
                    new FailingMetadataTransactionFactory(fixture.TransactionFactory, failRuleAction: true));

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
        Assert.AreEqual(0, (await fixture.FetchAccountStore.GetFetchAccountsAsync(1, CancellationToken.None).ConfigureAwait(false)).Count);
        Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_fetchaccounts_uids", "uidfaid", 1).ConfigureAwait(false));
        Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_rules", "ruleaccountid", 1).ConfigureAwait(false));
        Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_rule_criterias", "criteriaruleid", 1).ConfigureAwait(false));
        Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_rule_actions", "actionruleid", 1).ConfigureAwait(false));
        Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_imapfolders", "folderaccountid", 1).ConfigureAwait(false));
        Assert.AreEqual(0, await CountRowsAsync(fixture.ConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
    }

    private static string ToDbOnlyArchiveXml(string archiveXml) =>
        archiveXml
            .Replace("Mode=\"6\"", "Mode=\"2\"", StringComparison.Ordinal)
            .Replace("<DataFiles Format=\"Raw\" FolderName=\"DataBackup\" />", string.Empty, StringComparison.Ordinal);

    private static async Task WithFullRestoreTargetAsync(
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
            var dataBackup = Path.Combine(root, "DataBackup");
            var archivePath = Path.Combine(root, "backup.7z");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(dataBackup);
            File.WriteAllText(Path.Combine(dataDirectory, "original.txt"), "original");
            File.WriteAllText(Path.Combine(dataBackup, "restored.txt"), "restored");
            var messagePath = Path.Combine(dataBackup, "roundtrip.example", "user", "ne");
            Directory.CreateDirectory(messagePath);
            File.WriteAllText(Path.Combine(messagePath, "one.eml"), "From: sender@example.test\r\n\r\nbody one");
            File.WriteAllText(Path.Combine(messagePath, "two.eml"), "From: sender2@example.test\r\n\r\nbody two");
            File.WriteAllText(Path.Combine(source, "hMailServerBackup.xml"), archiveXml);
            await CreateArchiveAsync(archivePath, source).ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(testConnectionString);
            using var binding = BackupArchiveBinding.TryCreate(archivePath);
            Assert.IsNotNull(binding);
            var backup = Backup.CreateAuthorized(
                7,
                binding.ArchivePath,
                archiveIdentity: binding.Identity,
                archiveBinding: binding,
                rawDataBackupIdentity: binding.RawDataBackupIdentity);
            backup.RestoreSettings = true;
            backup.RestoreDomains = true;
            backup.RestoreMessages = true;
            await action(new DbOnlyRestoreFixture(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                archivePath,
                backup,
                testConnectionString,
                new SqlServerDomainAdministrationStore(factory),
                new SqlServerAccountAdministrationStore(factory),
                new SqlServerAliasAdministrationStore(factory),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                new SqlServerFetchAccountAdministrationStore(factory),
                new SqlServerBackupRestoreMetadataTransactionFactory(factory))).ConfigureAwait(false);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (!TryRetainTestRoot(root, name) && Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

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
                archivePath,
                backup,
                testConnectionString,
                new SqlServerDomainAdministrationStore(factory),
                new SqlServerAccountAdministrationStore(factory),
                new SqlServerAliasAdministrationStore(factory),
                new SqlServerDistributionListAdministrationStore(factory),
                new SqlServerDistributionListRecipientAdministrationStore(factory),
                new SqlServerFetchAccountAdministrationStore(factory),
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
        string archivePath,
        Backup backup,
        string connectionString,
        SqlServerDomainAdministrationStore domainStore,
        SqlServerAccountAdministrationStore accountStore,
        SqlServerAliasAdministrationStore aliasStore,
        SqlServerDistributionListAdministrationStore listStore,
        SqlServerDistributionListRecipientAdministrationStore recipientStore,
        SqlServerFetchAccountAdministrationStore fetchAccountStore,
        SqlServerBackupRestoreMetadataTransactionFactory transactionFactory)
    {
        internal Backup Backup { get; } = backup;
        internal string ArchivePath { get; } = archivePath;
        internal string GetDataDirectory() => dataDirectory;
        internal string ConnectionString { get; } = connectionString;
        internal SqlServerDomainAdministrationStore DomainStore { get; } = domainStore;
        internal SqlServerAccountAdministrationStore AccountStore { get; } = accountStore;
        internal SqlServerAliasAdministrationStore AliasStore { get; } = aliasStore;
        internal SqlServerDistributionListAdministrationStore ListStore { get; } = listStore;
        internal SqlServerDistributionListRecipientAdministrationStore RecipientStore { get; } = recipientStore;
        internal SqlServerFetchAccountAdministrationStore FetchAccountStore { get; } = fetchAccountStore;
        internal SqlServerBackupRestoreMetadataTransactionFactory TransactionFactory { get; } = transactionFactory;

        internal MetadataBackupRestoreExecutor CreateExecutor(
            IBackupRestoreMetadataTransactionFactory? transactionFactory = null,
            IBackupRestoreDataDirectoryMutation? filesystemMutation = null) =>
            new(
                sevenZipExecutablePath,
                dataDirectory,
                DomainStore,
                AccountStore,
                AliasStore,
                ListStore,
                RecipientStore,
                fetchAccountStore: FetchAccountStore,
                ruleStore: new SqlServerRuleAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                messageRestoreStore: new SqlServerMessageAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                messageStore: new SqlServerMessageAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                dataDirectoryRuntime: filesystemMutation is null
                    ? null
                    : new BackupRestoreDataDirectoryRuntime(
                        sevenZipExecutablePath,
                        filesystemMutation: filesystemMutation),
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
                fetchAccountStore: FetchAccountStore,
                ruleStore: new SqlServerRuleAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                ruleCriteriaStore: new SqlServerRuleCriteriaAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                ruleActionStore: new SqlServerRuleActionAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                folderRestoreStore: new SqlServerImapFolderAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                folderRestoreDeletionStore: new SqlServerImapFolderAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                messageRestoreStore: new SqlServerMessageAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                messageStore: new SqlServerMessageAdministrationStore(new SqlServerConnectionFactory(ConnectionString)),
                metadataTransactionFactory: null,
                requireSqlTransaction: true);

        internal async Task SeedExistingDomainAndPublicFolderAsync()
        {
            await DomainStore.InsertDomainAsync(
                new DomainAdministrationSnapshot(0, "stale.example", true, Postmaster: "postmaster@stale.example"),
                CancellationToken.None).ConfigureAwait(false);

            const string sql = """
                DECLARE @FolderId int;
                INSERT INTO dbo.hm_imapfolders
                    (folderaccountid, folderparentid, foldername, folderissubscribed, foldercurrentuid, foldercreationtime)
                VALUES (0, 0, N'#Shared', 1, 7, '2026-07-01 12:00:00');
                SET @FolderId = CONVERT(int, SCOPE_IDENTITY());
                INSERT INTO dbo.hm_messages
                    (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
                     messagesize, messagecurnooftries, messagenexttrytime, messageflags, messagecreatetime,
                     messagelocked, messageuid)
                VALUES (0, @FolderId, N'stale.eml', 0, N'stale@example.test', 12, 0,
                        '2026-07-01 12:00:00', 0, '2026-07-01 12:00:00', 0, 1);
                """;
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        internal async Task SeedExistingPublicFolderAsync()
        {
            const string sql = """
                DECLARE @FolderId int;
                INSERT INTO dbo.hm_imapfolders
                    (folderaccountid, folderparentid, foldername, folderissubscribed, foldercurrentuid, foldercreationtime)
                VALUES (0, -1, N'Shared', 1, 11, '2026-07-01 13:00:00');
                SET @FolderId = CONVERT(int, SCOPE_IDENTITY());
                INSERT INTO dbo.hm_messages
                    (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
                     messagesize, messagecurnooftries, messagenexttrytime, messageflags, messagecreatetime,
                     messagelocked, messageuid)
                VALUES (0, @FolderId, N'public.eml', 2, N'public@example.test', 19, 2,
                        '2026-07-01 13:00:00', 1, '2026-07-01 13:01:00', 0, 12);
                """;
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private sealed class DeterministicFilesystemMutation : IBackupRestoreDataDirectoryMutation
    {
        public void MoveDirectory(string sourcePath, string destinationPath) =>
            Directory.Move(sourcePath, destinationPath);
    }

    private sealed class RecordingBackupEventDispatcher(
        Func<bool>? completedProbe = null) : IBackupEventDispatcher
    {
        private int _completedCount;

        internal TaskCompletionSource<object?> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<object?> SecondCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<object?> ThirdCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<string> Failed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool CompletedArchiveExistsAtDispatch { get; private set; }

        public void OnBackupCompleted()
        {
            CompletedArchiveExistsAtDispatch = completedProbe?.Invoke() ?? false;
            switch (Interlocked.Increment(ref _completedCount))
            {
                case 1:
                    Completed.TrySetResult(null);
                    break;
                case 2:
                    SecondCompleted.TrySetResult(null);
                    break;
                default:
                    ThirdCompleted.TrySetResult(null);
                    break;
            }
        }

        public void OnBackupFailed(string reason) => Failed.TrySetResult(reason);
    }

    private static async Task WaitForBackupCompletionAsync(
        Task completed,
        Task<string> failed)
    {
        var result = await Task.WhenAny(completed, failed).ConfigureAwait(false);
        if (ReferenceEquals(result, failed))
        {
            Assert.Fail("The backup/restore operation failed: " + failed.Result);
        }
    }

    private static string NormalizeBackupXml(string xml)
    {
        var document = System.Xml.Linq.XDocument.Parse(xml);
        foreach (var element in document.Descendants())
        {
            var attributes = element
                .Attributes()
                .Where(static attribute =>
                    !attribute.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase)
                    && !attribute.Name.LocalName.Equals("ID", StringComparison.OrdinalIgnoreCase)
                    && !attribute.Name.LocalName.Equals("CreateTime", StringComparison.OrdinalIgnoreCase)
                    && !attribute.Name.LocalName.Equals("LastLogonTime", StringComparison.OrdinalIgnoreCase)
                    && !attribute.Name.LocalName.Equals("Date", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static attribute => attribute.Name.LocalName, StringComparer.Ordinal)
                .Select(static attribute => new System.Xml.Linq.XAttribute(attribute.Name, attribute.Value))
                .ToArray();
            element.ReplaceAttributes(attributes);
        }

        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static string[] GetDataBackupEvidence(string dataBackupRoot)
    {
        Assert.IsTrue(
            Directory.Exists(dataBackupRoot),
            dataBackupRoot);
        return Directory
            .EnumerateFiles(dataBackupRoot, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(dataBackupRoot, path).Replace('\\', '/');
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
                return relativePath + ":" + hash;
            })
            .OrderBy(static evidence => evidence, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class FailingMetadataTransactionFactory(
        IBackupRestoreMetadataTransactionFactory inner,
        bool failAlias = false,
        bool failSecondRecipient = false,
        bool failRuleAction = false,
        bool failSecondMessage = false,
        bool throwAfterCommit = false) : IBackupRestoreMetadataTransactionFactory
    {
        public async ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await inner.BeginAsync(cancellationToken).ConfigureAwait(false);
            return new FailingMetadataTransaction(
                transaction,
                failAlias,
                failSecondRecipient,
                failRuleAction,
                failSecondMessage,
                throwAfterCommit);
        }
    }

    private sealed class FailingMetadataTransaction(
        IBackupRestoreMetadataTransaction inner,
        bool failAlias,
        bool failSecondRecipient,
        bool failRuleAction,
        bool failSecondMessage,
        bool throwAfterCommit) : IBackupRestoreMetadataTransaction
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
        public ISecurityRangeAdministrationStore? SecurityRangeStore => inner.SecurityRangeStore;
        public ITcpIpPortAdministrationStore? TcpIpPortStore => inner.TcpIpPortStore;
        public IBlockedAttachmentAdministrationStore? BlockedAttachmentStore => inner.BlockedAttachmentStore;
        public ISurblServerAdministrationStore? SurblServerStore => inner.SurblServerStore;
        public IDnsBlackListAdministrationStore? DnsBlackListStore => inner.DnsBlackListStore;
        public IFetchAccountAdministrationStore? FetchAccountStore => inner.FetchAccountStore;
        public IRuleAdministrationStore? RuleStore => inner.RuleStore;
        public IRuleCriteriaAdministrationStore? RuleCriteriaStore => inner.RuleCriteriaStore;
        public IRuleActionAdministrationStore? RuleActionStore => failRuleAction
            ? new FailingRuleActionAdministrationStore(inner.RuleActionStore!)
            : inner.RuleActionStore;
        public IImapFolderAdministrationRestoreStore? FolderRestoreStore => inner.FolderRestoreStore;

        public IMessageAdministrationRestoreStore? MessageRestoreStore => failSecondMessage
            ? new FailingOnSecondMessageAdministrationRestoreStore(inner.MessageRestoreStore!)
            : inner.MessageRestoreStore;
        public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllDomainsForRestoreAsync(cancellationToken);
        public ValueTask DeleteAllPublicFoldersForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllPublicFoldersForRestoreAsync(cancellationToken);
        public ValueTask DeleteAllSecurityRangesForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllSecurityRangesForRestoreAsync(cancellationToken);
        public ValueTask DeleteAllTcpIpPortsForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllTcpIpPortsForRestoreAsync(cancellationToken);
        public ValueTask DeleteAllBlockedAttachmentsForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllBlockedAttachmentsForRestoreAsync(cancellationToken);
        public ValueTask DeleteAllSurblServersForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllSurblServersForRestoreAsync(cancellationToken);
        public ValueTask DeleteAllDnsBlackListsForRestoreAsync(CancellationToken cancellationToken) =>
            inner.DeleteAllDnsBlackListsForRestoreAsync(cancellationToken);
        public async ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            await inner.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (throwAfterCommit)
            {
                throw new InvalidOperationException("The SQL commit outcome is ambiguous.");
            }
        }
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

    private sealed class FailingMessageAdministrationRestoreStore : IMessageAdministrationRestoreStore
    {
        public ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
            int accountId,
            int folderId,
            MessageAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<MessageAdministrationInsertResult>(
                new InvalidOperationException("Simulated message restore failure."));
    }

    private sealed class FailingOnSecondMessageAdministrationRestoreStore(
        IMessageAdministrationRestoreStore inner) : IMessageAdministrationRestoreStore
    {
        private int _insertCount;

        public async ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
            int accountId,
            int folderId,
            MessageAdministrationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            if (++_insertCount == 2)
            {
                throw new InvalidOperationException("Simulated second message restore failure.");
            }

            return await inner.InsertMessageForRestoreAsync(
                accountId,
                folderId,
                snapshot,
                cancellationToken).ConfigureAwait(false);
        }
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

    private sealed class FailingRuleActionAdministrationStore(IRuleActionAdministrationStore inner)
        : IRuleActionAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
            int ruleId,
            CancellationToken cancellationToken) => inner.GetRuleActionsAsync(ruleId, cancellationToken);

        public ValueTask DeleteRuleActionByIdAsync(
            int ruleId,
            int databaseId,
            CancellationToken cancellationToken) => inner.DeleteRuleActionByIdAsync(ruleId, databaseId, cancellationToken);

        public ValueTask<int> InsertRuleActionAsync(
            int owningRuleId,
            RuleActionAdministrationSnapshot action,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<int>(new InvalidOperationException("Simulated rule action restore failure."));

        public ValueTask SaveRuleActionAsync(
            int owningRuleId,
            RuleActionAdministrationSnapshot action,
            CancellationToken cancellationToken) => inner.SaveRuleActionAsync(owningRuleId, action, cancellationToken);

        public ValueTask SaveRuleActionOrderAsync(
            int owningRuleId,
            IReadOnlyList<RuleActionAdministrationSnapshot> actions,
            CancellationToken cancellationToken) => inner.SaveRuleActionOrderAsync(owningRuleId, actions, cancellationToken);
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
            CREATE TABLE dbo.hm_settings (
                settingname nvarchar(30) NOT NULL PRIMARY KEY,
                settingstring nvarchar(4000) NOT NULL,
                settinginteger bigint NOT NULL
            );
            INSERT INTO dbo.hm_settings (settingname, settingstring, settinginteger)
            VALUES (N'welcomesmtp', N'old greeting', 0);
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
                messagefolderid int NOT NULL,
                messagefilename nvarchar(255) NOT NULL,
                messagetype tinyint NOT NULL,
                messagefrom nvarchar(255) NOT NULL,
                messagesize bigint NOT NULL,
                messagecurnooftries int NOT NULL,
                messagenexttrytime datetime NOT NULL,
                messageflags tinyint NOT NULL,
                messagecreatetime datetime NOT NULL,
                messagelocked tinyint NOT NULL,
                messageuid bigint NOT NULL
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
                daid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                dadomainid int NOT NULL,
                daalias nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_rules (
                ruleid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                ruleaccountid int NOT NULL,
                rulename nvarchar(255) NOT NULL,
                ruleactive tinyint NOT NULL,
                ruleuseand tinyint NOT NULL,
                rulesortorder int NOT NULL
            );
            CREATE TABLE dbo.hm_rule_actions (
                actionid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                actionruleid int NOT NULL,
                actiontype tinyint NOT NULL,
                actionsubject nvarchar(255) NOT NULL,
                actionbody nvarchar(max) NOT NULL,
                actionfromname nvarchar(255) NOT NULL,
                actionfromaddress nvarchar(255) NOT NULL,
                actionfilename nvarchar(255) NOT NULL,
                actionto nvarchar(255) NOT NULL,
                actionimapfolder nvarchar(255) NOT NULL,
                actionscriptfunction nvarchar(255) NOT NULL,
                actionheader nvarchar(80) NOT NULL,
                actionvalue nvarchar(255) NOT NULL,
                actionrouteid int NOT NULL,
                actionabortspamflagged tinyint NOT NULL,
                actionsortorder int NOT NULL
            );
            CREATE TABLE dbo.hm_rule_criterias (
                criteriaid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                criteriaruleid int NOT NULL,
                criteriamatchvalue nvarchar(255) NOT NULL,
                criteriausepredefined tinyint NOT NULL,
                criteriapredefinedfield tinyint NOT NULL,
                criteriamatchtype tinyint NOT NULL,
                criteriaheadername nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_messagerecipients (
                recipientmessageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_metadata (
                metadata_accountid int NOT NULL,
                metadata_messageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_search_queue (
                messageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_search_documents (
                messageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_fetchaccounts (
                faid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                faaccountid int NOT NULL,
                faaccountname nvarchar(255) NOT NULL,
                faserveraddress nvarchar(255) NOT NULL,
                faserverport int NOT NULL,
                faservertype tinyint NOT NULL,
                fausername nvarchar(255) NOT NULL,
                fapassword nvarchar(255) NOT NULL,
                faminutes int NOT NULL,
                fanexttry datetime NOT NULL,
                fadaystokeep int NOT NULL,
                faactive tinyint NOT NULL,
                falocked tinyint NOT NULL,
                faprocessmimerecipients tinyint NOT NULL,
                faprocessmimedate tinyint NOT NULL,
                faconnectionsecurity tinyint NOT NULL,
                fauseantispam tinyint NOT NULL,
                fauseantivirus tinyint NOT NULL,
                faenablerouterecipients tinyint NOT NULL,
                famimerecipientheaders nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_imapfolders (
                folderid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                folderaccountid int NOT NULL,
                folderparentid int NOT NULL,
                foldername nvarchar(255) NOT NULL,
                folderissubscribed tinyint NOT NULL,
                foldercurrentuid int NOT NULL,
                foldercreationtime datetime NOT NULL
            );
            CREATE TABLE dbo.hm_acl (
                aclid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                aclsharefolderid bigint NOT NULL,
                aclpermissiontype tinyint NOT NULL,
                aclpermissiongroupid bigint NOT NULL,
                aclpermissionaccountid bigint NOT NULL,
                aclvalue bigint NOT NULL
            );
            CREATE TABLE dbo.hm_groups (
                groupid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                groupname nvarchar(255) NULL
            );
            CREATE TABLE dbo.hm_group_members (
                memberid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                membergroupid bigint NOT NULL,
                memberaccountid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_fetchaccounts_uids (
                uidid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                uidfaid int NOT NULL,
                uidvalue nvarchar(255) NOT NULL,
                uidtime datetime NOT NULL
            );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<long> CountRowsAsync(
        string connectionString,
        string tableName,
        string columnName,
        int value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName} WHERE {columnName} = @Value;",
            connection);
        command.Parameters.Add("@Value", SqlDbType.Int).Value = value;
        return Convert.ToInt64(
            await command.ExecuteScalarAsync().ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountRowsAsync(string connectionString, string tableName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName};",
            connection);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync().ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountRowsByNameAsync(
        string connectionString,
        string tableName,
        string columnName,
        string value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName} WHERE {columnName} = @Value;",
            connection);
        command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = value;
        return Convert.ToInt64(
            await command.ExecuteScalarAsync().ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static async Task<int> SeedGroupGraphAsync(string connectionString, string groupName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var groupCommand = new SqlCommand(
            "INSERT INTO dbo.hm_groups (groupname) OUTPUT INSERTED.groupid VALUES (@Name);",
            connection);
        groupCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = groupName;
        var groupId = Convert.ToInt32(
            await groupCommand.ExecuteScalarAsync().ConfigureAwait(false),
            CultureInfo.InvariantCulture);

        await using var memberCommand = new SqlCommand(
            "INSERT INTO dbo.hm_group_members (membergroupid, memberaccountid) VALUES (@GroupId, 41);",
            connection);
        memberCommand.Parameters.Add("@GroupId", SqlDbType.BigInt).Value = groupId;
        await memberCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

        await using var aclCommand = new SqlCommand(
            "INSERT INTO dbo.hm_acl (aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid, aclvalue) " +
            "VALUES (1, 1, @GroupId, 0, 1);",
            connection);
        aclCommand.Parameters.Add("@GroupId", SqlDbType.BigInt).Value = groupId;
        await aclCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        return groupId;
    }

    private static async Task<string> ReadSettingStringAsync(string connectionString, string settingName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT settingstring FROM dbo.hm_settings WHERE settingname = @Name;",
            connection);
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 30).Value = settingName;
        return (string)(await command.ExecuteScalarAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Missing setting {settingName}."));
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
