using System.Diagnostics;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreExecutionTests
{
    private const string SettingsArchiveXml = """
        <Backup>
          <BackupInformation Mode="1" />
          <Properties>
            <hostname LongValue="0" StringValue="restored.example" />
            <maxsmtpconnections LongValue="25" StringValue="" />
          </Properties>
        </Backup>
        """;

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

    private const string FullPublicArchiveXml = """
        <Backup>
          <BackupInformation Mode="7"><DataFiles Format="Raw" FolderName="DataBackup" /></BackupInformation>
          <Properties><hostname LongValue="0" StringValue="restored.example" /></Properties>
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
            </Domain>
          </Domains>
          <PublicFolders>
            <Folder Name="Shared" Subscribed="1" CreateTime="2026-08-20 01:02:03" CurrentUID="7">
              <Messages>
                <Message CreateTime="2026-08-20 04:05:06" Filename="restored.eml"
                         FromAddress="from@example.test" State="2" Size="10" NoOfRetries="0"
                         Flags="0" ID="601" UID="9" />
              </Messages>
              <Folders>
                <Folder Name="Child" Subscribed="0" CreateTime="2026-08-20 02:03:04" CurrentUID="8" />
              </Folders>
              <ACLs><Permission Type="0" Rights="3" Holder="alice@restore.example" /></ACLs>
            </Folder>
          </PublicFolders>
        </Backup>
        """;

    private const string SettingsSecurityRangesArchiveXml = """
        <Backup>
          <BackupInformation Mode="1" />
          <Properties><hostname LongValue="0" StringValue="restored.example" /></Properties>
          <SecurityRanges>
            <SecurityRange Name="first" LowerIP="10.0.0.1" UpperIP="10.0.0.9"
                           Priority="7" Options="11" ExpiresTime="2026-07-01 12:30:00" Expires="1" />
            <SecurityRange Name="second" LowerIP="10.0.0.10" UpperIP="10.0.0.19"
                           Priority="3" Options="5" ExpiresTime="2026-07-02 12:30:00" Expires="0" />
          </SecurityRanges>
        </Backup>
        """;

    private const string SettingsTcpIpPortsArchiveXml = """
        <Backup>
          <BackupInformation Mode="1" />
          <Properties><hostname LongValue="0" StringValue="restored.example" /></Properties>
          <TCPIPPorts>
            <TCPIPPort Name="smtp" PortProtocol="1" PortNumber="25"
                       ConnectionSecurity="0" Address="0.0.0.0" />
            <TCPIPPort Name="imap" PortProtocol="2" PortNumber="993"
                       ConnectionSecurity="1" Address="127.0.0.1"
                       SSLCertificateName="imap-cert" />
          </TCPIPPorts>
        </Backup>
        """;

    private const string SettingsBlockedAttachmentsArchiveXml = """
        <Backup>
          <BackupInformation Mode="1" />
          <Properties><hostname LongValue="0" StringValue="restored.example" /></Properties>
          <BlockedAttachments>
            <BlockedAttachment Name="*.exe" Description="Executable" />
            <BlockedAttachment Name="*.zip" Description="Archive" />
          </BlockedAttachments>
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
    public async Task ExecuteAsync_RestoresPublicFoldersAfterAccountsInsideTheSqlTransaction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(
            compressed: false,
            customXml: FullPublicArchiveXml);
        var stores = new RecordingStores();
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(7, fixture.ArchivePath);
        backup.RestoreSettings = true;
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Shared", "Child" },
            stores.PublicFolders.Inserted.Select(static folder => folder.Name).ToArray());
        Assert.AreEqual(0, stores.PublicFolders.Inserted[0].AccountId);
        Assert.AreEqual(0, stores.PublicMessages.Inserted[0].AccountId);
        Assert.AreEqual(1, stores.PublicPermissions.Inserted.Count);
        Assert.AreEqual(1, stores.PublicPermissions.Inserted[0].PermissionAccountId);
        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_RestoresGroupsBeforePublicFolderGroupAcl()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var archiveXml = FullPublicArchiveXml
            .Replace(
                "<Permission Type=\"0\" Rights=\"3\" Holder=\"alice@restore.example\" />",
                "<Permission Type=\"1\" Rights=\"3\" Holder=\"Editors\" />",
                StringComparison.Ordinal)
            .Replace(
                "</Backup>",
                "<Groups><Group Name=\"Editors\"><GroupMembers><Member Name=\"alice@restore.example\" /></GroupMembers></Group></Groups></Backup>",
                StringComparison.Ordinal);
        using var fixture = await ArchiveFixture.CreateNonDbAsync(
            compressed: false,
            customXml: archiveXml);
        var stores = new RecordingStores();
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(7, fixture.ArchivePath);
        backup.RestoreSettings = true;
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, stores.Groups.Inserted.Count);
        Assert.AreEqual("Editors", stores.Groups.Inserted[0].Name);
        CollectionAssert.AreEqual(
            new[] { (GroupId: 77, AccountId: 1) },
            stores.GroupMembers.Inserted.Select(static member =>
                (GroupId: member.GroupId, AccountId: member.AccountId)).ToArray());
        CollectionAssert.AreEqual(
            new[] { (GroupId: 77, AccountId: 0) },
            stores.PublicPermissions.Inserted.Select(static permission =>
                (GroupId: permission.PermissionGroupId, AccountId: permission.PermissionAccountId)).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_GroupMemberFailureRollsBackFullRestoreBeforeCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var archiveXml = FullPublicArchiveXml
            .Replace(
                "<Permission Type=\"0\" Rights=\"3\" Holder=\"alice@restore.example\" />",
                "<Permission Type=\"1\" Rights=\"3\" Holder=\"Editors\" />",
                StringComparison.Ordinal)
            .Replace(
                "</Backup>",
                "<Groups><Group Name=\"Editors\"><GroupMembers><Member Name=\"alice@restore.example\" /></GroupMembers></Group></Groups></Backup>",
                StringComparison.Ordinal);
        using var fixture = await ArchiveFixture.CreateNonDbAsync(
            compressed: false,
            customXml: archiveXml);
        var stores = new RecordingStores { FailGroupMemberInsert = true };
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(7, fixture.ArchivePath);
        backup.RestoreSettings = true;
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        var transaction = transactionFactory.LastTransaction!;
        Assert.AreEqual(1, stores.GroupMembers.InsertAttempts);
        Assert.IsTrue(transaction.Disposed);
        Assert.IsTrue(transaction.RolledBack);
        Assert.AreEqual(0, transaction.CommitCount);
        Assert.AreEqual(0, stores.Groups.Inserted.Count);
        Assert.AreEqual(0, stores.GroupMembers.Inserted.Count);
        Assert.AreEqual(0, stores.PublicPermissions.Inserted.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_InvokesInjectedReinitializeOnceAfterSuccessfulRestore()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        var reinitializeCount = 0;
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            _ =>
            {
                reinitializeCount++;
                return ValueTask.CompletedTask;
            });
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, reinitializeCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DoesNotInvokeInjectedReinitializeWhenRestoreFails()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores { FailAliasInsert = true };
        var reinitializeCount = 0;
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            _ =>
            {
                reinitializeCount++;
                return ValueTask.CompletedTask;
            });
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(0, reinitializeCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailsClosedBeforeMutationWhenProductionReinitializeIsMissing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            requireReinitialize: true);
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        StringAssert.Contains(error.Message, "reinitialization is not configured");
        Assert.AreEqual(0, stores.Domains.Items.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_RestoresSettingsOnlyInsideTheSqlTransaction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsArchiveXml);
        var stores = new RecordingStores();
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new BackupSettingsPropertySnapshot("hostname", 0, "restored.example"),
                new BackupSettingsPropertySnapshot("maxsmtpconnections", 25, string.Empty)
            },
            stores.Settings.Properties.ToArray());
        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SettingsFailureDisposesTransactionWithoutCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsArchiveXml);
        var stores = new RecordingStores();
        stores.Settings.Fail = true;
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        Assert.AreEqual(0, stores.Settings.Properties.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_SettingsOnlyReplacesSecurityRangesInsideTheSqlTransaction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsSecurityRangesArchiveXml);
        var stores = new RecordingStores();
        stores.SecurityRanges.Items.Add(
            new SecurityRangeAdministrationSnapshot(99, "old", "192.0.2.1", "192.0.2.2", 1, 0, false, new DateTime(2026, 1, 1)));
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, transactionFactory.SecurityRangeDeleteCount);
        CollectionAssert.AreEqual(
            new[] { "first", "second" },
            stores.SecurityRanges.Items.Select(static range => range.Name).ToArray());
        Assert.AreEqual(0, stores.SecurityRanges.Items[0].Id);
        Assert.AreEqual(2, stores.SecurityRanges.InsertAttempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecurityRangeFailureDisposesTransactionWithoutCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsSecurityRangesArchiveXml);
        var stores = new RecordingStores();
        stores.SecurityRanges.Fail = true;
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(1, transactionFactory.SecurityRangeDeleteCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        Assert.IsTrue(transactionFactory.LastTransaction.RolledBack);
        Assert.AreEqual(0, transactionFactory.LastTransaction.CommitCount);
        Assert.IsEmpty(stores.SecurityRanges.Items);
    }

    [TestMethod]
    public async Task ExecuteAsync_SettingsOnlyReplacesTcpIpPortsInsideTheSqlTransaction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsTcpIpPortsArchiveXml);
        var stores = new RecordingStores();
        stores.TcpIpPorts.Items.Add(new TcpIpPortAdministrationSnapshot(
            99, 1, 2525, "192.0.2.1", 0, 0));
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, transactionFactory.TcpIpPortDeleteCount);
        Assert.AreEqual(2, stores.TcpIpPorts.InsertAttempts);
        Assert.AreEqual(2, stores.TcpIpPorts.Items.Count);
        Assert.AreEqual(25, stores.TcpIpPorts.Items[0].PortNumber);
        Assert.AreEqual(993, stores.TcpIpPorts.Items[1].PortNumber);
        Assert.AreEqual("imap-cert", stores.TcpIpPorts.Items[1].SslCertificateName);
    }

    [TestMethod]
    public async Task ExecuteAsync_TcpIpPortFailureDisposesTransactionWithoutCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsTcpIpPortsArchiveXml);
        var stores = new RecordingStores();
        stores.TcpIpPorts.Fail = true;
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(1, transactionFactory.TcpIpPortDeleteCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        Assert.IsTrue(transactionFactory.LastTransaction.RolledBack);
        Assert.AreEqual(0, transactionFactory.LastTransaction.CommitCount);
        Assert.IsEmpty(stores.TcpIpPorts.Items);
    }

    [TestMethod]
    public async Task ExecuteAsync_SettingsOnlyReplacesBlockedAttachmentsInsideTheSqlTransaction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsBlockedAttachmentsArchiveXml);
        var stores = new RecordingStores();
        stores.BlockedAttachments.Items.Add(
            new BlockedAttachmentAdministrationSnapshot(99, "*.old", "Old"));
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        Assert.AreEqual(1, transactionFactory.BlockedAttachmentDeleteCount);
        Assert.AreEqual(2, stores.BlockedAttachments.InsertAttempts);
        CollectionAssert.AreEqual(
            new[] { "*.exe", "*.zip" },
            stores.BlockedAttachments.Items.Select(static item => item.Wildcard).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_BlockedAttachmentFailureDisposesTransactionWithoutCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(SettingsBlockedAttachmentsArchiveXml);
        var stores = new RecordingStores();
        stores.BlockedAttachments.Fail = true;
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = stores.CreateExecutor(
            fixture.DataDirectory,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
        backup.RestoreSettings = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(1, transactionFactory.BlockedAttachmentDeleteCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        Assert.IsTrue(transactionFactory.LastTransaction.RolledBack);
        Assert.AreEqual(0, transactionFactory.LastTransaction.CommitCount);
        Assert.IsEmpty(stores.BlockedAttachments.Items);
    }

    [TestMethod]
    public async Task ExecuteAsync_RestoresCombinedDomainsAndSettingsInLegacyOrder()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var archiveXml = ArchiveXml
            .Replace("Mode=\"2\"", "Mode=\"3\"", StringComparison.Ordinal)
            .Replace(
                "</Backup>",
                "  <Properties><hostname LongValue=\"0\" StringValue=\"combined.example\" /></Properties>\n</Backup>",
                StringComparison.Ordinal);
        using var fixture = await ArchiveFixture.CreateAsync(archiveXml);
        var stores = new RecordingStores();
        stores.Domains.EventSink = eventName => stores.Events.Add(eventName);
        stores.Settings.OnRestore = () => stores.Events.Add("restore-settings");
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(3, fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreSettings = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "delete-all-domains", "delete-all-groups", "insert-domain", "restore-settings" },
            stores.Events.ToArray());
        Assert.AreEqual("combined.example", stores.Settings.Properties.Single().StringValue);
        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SettingsOnlyClearsGroupsWhenGroupsContainerIsEmptyOrOmitted()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var archives = new[]
        {
            SettingsArchiveXml,
            SettingsArchiveXml.Replace(
                "</Backup>",
                "<Groups /></Backup>",
                StringComparison.Ordinal)
        };

        foreach (var archiveXml in archives)
        {
            using var fixture = await ArchiveFixture.CreateAsync(archiveXml);
            var stores = new RecordingStores();
            var transactionFactory = new RecordingMetadataTransactionFactory(stores);
            var executor = new MetadataBackupRestoreExecutor(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                fixture.DataDirectory,
                stores.Domains,
                stores.Accounts,
                stores.Aliases,
                stores.DistributionLists,
                stores.Recipients,
                metadataTransactionFactory: transactionFactory,
                requireSqlTransaction: true);
            var backup = Backup.CreateAuthorized(1, fixture.ArchivePath);
            backup.RestoreSettings = true;

            await executor.ExecuteAsync(backup, CancellationToken.None);

            Assert.AreEqual(1, stores.Events.Count(static eventName => eventName == "delete-all-groups"));
            Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CombinedSettingsFailureDisposesTransactionBeforeCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var archiveXml = ArchiveXml
            .Replace("Mode=\"2\"", "Mode=\"3\"", StringComparison.Ordinal)
            .Replace(
                "</Backup>",
                "  <Properties><hostname LongValue=\"0\" StringValue=\"combined.example\" /></Properties>\n</Backup>",
                StringComparison.Ordinal);
        using var fixture = await ArchiveFixture.CreateAsync(archiveXml);
        var stores = new RecordingStores();
        stores.Settings.Fail = true;
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(3, fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreSettings = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        Assert.AreEqual(0, stores.Settings.Properties.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_DbOnlyRestoreDeletesExistingDomainsInsideTransactionBeforeInsert()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        stores.Events = [];
        stores.Domains.EventSink = eventName => stores.Events.Add(eventName);
        stores.Domains.Items.Add(new DomainAdministrationSnapshot(Id: 77, Name: "restore.example", Active: true));
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await executor.ExecuteAsync(backup, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "delete-all-domains", "insert-domain" }, stores.Events);
        Assert.AreEqual(1, stores.Domains.Items.Count);
        Assert.AreEqual("restore.example", stores.Domains.Items[0].Name);
        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.AreEqual(1, transactionFactory.DeleteCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DbOnlyRestoreDeleteFailureStopsBeforeMetadataInsertAndDisposesTransaction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        var transactionFactory = new RecordingMetadataTransactionFactory(stores) { FailDelete = true };
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = Backup.CreateAuthorized(2, fixture.ArchivePath);
        backup.RestoreDomains = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        Assert.AreEqual(1, transactionFactory.BeginCount);
        Assert.AreEqual(1, transactionFactory.DeleteCount);
        Assert.IsTrue(transactionFactory.LastTransaction!.Disposed);
        Assert.AreEqual(0, stores.Domains.Items.Count);
        Assert.AreEqual(0, stores.Accounts.Items.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsDbOnlyRestoreWhenAuthorizationIsInvalidatedBeforeLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        var readStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedDomainStore = new GatedDomainStore(stores.Domains, readStarted, releaseRead);
        var transactionFactory = new RecordingMetadataTransactionFactory(stores);
        var application = CreateAuthenticatedApplication(new RecordingBackupArchiveMetadataReader(2));
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            gatedDomainStore,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = (Backup)application.BackupManager.LoadBackup(fixture.ArchivePath);
        backup.RestoreDomains = true;

        var restoreTask = executor.ExecuteAsync(backup, CancellationToken.None).AsTask();
        await readStarted.Task;
        Assert.IsNull(application.Authenticate("administrator", "wrong"));
        releaseRead.SetResult(null);

        var error = await Assert.ThrowsExactlyAsync<COMException>(
            () => restoreTask);

        Assert.AreEqual(unchecked((int)0x80070005), error.ErrorCode);
        Assert.AreEqual(0, transactionFactory.BeginCount);
        Assert.AreEqual(0, stores.Domains.Items.Count);
        Assert.AreEqual(0, stores.Accounts.Items.Count);
        Assert.AreEqual(0, stores.Aliases.Items.Count);
        Assert.AreEqual(0, stores.DistributionLists.Items.Count);
        Assert.AreEqual(0, stores.Recipients.Items.Count);
        backup.CleanupArchiveBinding();
    }

    [TestMethod]
    public async Task ExecuteAsync_CompletesUnderLeaseBeforeInvalidationAndThenRetainedBackupIsUnauthorized()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(ArchiveXml);
        var stores = new RecordingStores();
        var commitStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommit = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transactionFactory = new CommitGatedMetadataTransactionFactory(stores, commitStarted, releaseCommit);
        var application = CreateAuthenticatedApplication(new RecordingBackupArchiveMetadataReader(2));
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            metadataTransactionFactory: transactionFactory,
            requireSqlTransaction: true);
        var backup = (Backup)application.BackupManager.LoadBackup(fixture.ArchivePath);
        backup.RestoreDomains = true;

        var restoreTask = executor.ExecuteAsync(backup, CancellationToken.None).AsTask();
        await commitStarted.Task;
        var invalidationTask = Task.Run(() => application.Authenticate("administrator", "wrong"));

        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => invalidationTask.Status == TaskStatus.Running,
                TimeSpan.FromSeconds(1)));
        Assert.IsFalse(invalidationTask.IsCompleted);
        releaseCommit.SetResult(null);

        await restoreTask;
        Assert.IsNull(await invalidationTask);
        Assert.AreEqual(1, stores.Domains.Items.Count);
        Assert.AreEqual(
            unchecked((int)0x80070005),
            Assert.ThrowsExactly<COMException>(() => _ = backup.ContainsDomains).ErrorCode);
        backup.CleanupArchiveBinding();
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsNonDbRestoreWhenAuthorizationIsInvalidatedBeforeLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: false);
        var stores = new RecordingStores();
        var barrierEntered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBarrier = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rollbackPath = Path.Combine(fixture.Root, "rollback");
        var copyCount = 0;
        var dataDirectoryRuntime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            (_, _, _) => Interlocked.Increment(ref copyCount),
            filesystemMutation: new DeterministicFilesystemMutation());
        var application = CreateAuthenticatedApplication(new RecordingBackupArchiveMetadataReader(6));
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            dataDirectoryRuntime: dataDirectoryRuntime,
            dataDirectoryBoundaryFactory: () =>
            {
                barrierEntered.TrySetResult(null);
                releaseBarrier.Task.GetAwaiter().GetResult();
                return new BackupRestoreDataDirectoryBoundary(fixture.DataDirectory, rollbackPath);
            });
        var backup = (Backup)application.BackupManager.LoadBackup(fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        var restoreTask = Task.Run(
            async () => await executor.ExecuteAsync(backup, CancellationToken.None).ConfigureAwait(false));
        await barrierEntered.Task;
        Assert.IsNull(application.Authenticate("administrator", "wrong"));
        releaseBarrier.SetResult(null);

        var error = await Assert.ThrowsExactlyAsync<COMException>(() => restoreTask);

        Assert.AreEqual(unchecked((int)0x80070005), error.ErrorCode);
        Assert.AreEqual(0, copyCount);
        Assert.AreEqual("original", await File.ReadAllTextAsync(fixture.OriginalFilePath));
        Assert.IsFalse(File.Exists(fixture.RestoredFilePath));
        Assert.IsFalse(Directory.Exists(rollbackPath));
        Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.DataDirectory)));
        Assert.AreEqual(0, stores.Domains.Items.Count);
        Assert.AreEqual(0, stores.Accounts.Items.Count);
        Assert.AreEqual(0, stores.Aliases.Items.Count);
        Assert.AreEqual(0, stores.DistributionLists.Items.Count);
        Assert.AreEqual(0, stores.Recipients.Items.Count);
        backup.CleanupArchiveBinding();
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsNonDbRestoreWhenRawSourceChangesAfterAuthorizationLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: false);
        var stores = new RecordingStores();
        var copyCount = 0;
        var dataDirectoryRuntime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            (_, _, _) => Interlocked.Increment(ref copyCount),
            filesystemMutation: new DeterministicFilesystemMutation());
        var sourcePath = Path.Combine(fixture.Root, "DataBackup");
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            dataDirectoryRuntime: dataDirectoryRuntime);
        var backup = Backup.CreateAuthorized(
            6,
            fixture.ArchivePath,
            authorizationLeaseFactory: _ =>
            {
                Directory.Delete(sourcePath, recursive: true);
                return ValueTask.FromResult<IDisposable?>(new NoopLease());
            });
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        StringAssert.Contains(error.Message, "accessible directory");
        Assert.AreEqual(0, copyCount);
        Assert.AreEqual("original", await File.ReadAllTextAsync(fixture.OriginalFilePath));
        Assert.IsFalse(File.Exists(fixture.RestoredFilePath));
        Assert.AreEqual(0, stores.Domains.Items.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsNonDbRestoreWhenTargetPathChangesAfterAuthorizationLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: false);
        var stores = new RecordingStores();
        var copyCount = 0;
        var dataDirectoryRuntime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            (_, _, _) => Interlocked.Increment(ref copyCount),
            filesystemMutation: new DeterministicFilesystemMutation());
        var originalTargetPath = fixture.DataDirectory + ".original";
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            stores.Recipients,
            dataDirectoryRuntime: dataDirectoryRuntime);
        var backup = Backup.CreateAuthorized(
            6,
            fixture.ArchivePath,
            authorizationLeaseFactory: _ =>
            {
                Directory.Move(fixture.DataDirectory, originalTargetPath);
                File.WriteAllText(fixture.DataDirectory, "target mutation");
                return ValueTask.FromResult<IDisposable?>(new NoopLease());
            });
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(backup, CancellationToken.None).AsTask());

        StringAssert.Contains(error.Message, "target data path is a file");
        Assert.AreEqual(0, copyCount);
        Assert.AreEqual("target mutation", await File.ReadAllTextAsync(fixture.DataDirectory));
        Assert.AreEqual("original", await File.ReadAllTextAsync(Path.Combine(originalTargetPath, "original.txt")));
        Assert.AreEqual(0, stores.Domains.Items.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_HoldsNonDbLeaseThroughCopyAndMetadataCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateNonDbAsync(compressed: false);
        var stores = new RecordingStores();
        var copyStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCopy = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadataCommitStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMetadataCommit = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var copyCount = 0;
        var dataDirectoryRuntime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            (sourcePath, targetPath, cancellationToken) =>
            {
                Interlocked.Increment(ref copyCount);
                copyStarted.TrySetResult(null);
                releaseCopy.Task.GetAwaiter().GetResult();
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(
                    Path.Combine(sourcePath, "restored.txt"),
                    Path.Combine(targetPath, "restored.txt"));
            },
            filesystemMutation: new DeterministicFilesystemMutation());
        var application = CreateAuthenticatedApplication(new RecordingBackupArchiveMetadataReader(6));
        var backup = (Backup)application.BackupManager.LoadBackup(fixture.ArchivePath);
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;
        var executor = new MetadataBackupRestoreExecutor(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            fixture.DataDirectory,
            stores.Domains,
            stores.Accounts,
            stores.Aliases,
            stores.DistributionLists,
            new GatedRecipientStore(stores.Recipients, metadataCommitStarted, releaseMetadataCommit),
            dataDirectoryRuntime: dataDirectoryRuntime);

        var restoreTask = Task.Run(
            async () => await executor.ExecuteAsync(backup, CancellationToken.None).ConfigureAwait(false));
        Task<IInterfaceAccount?>? invalidationTask = null;
        var invalidationBlockedDuringCopy = false;
        var invalidationBlockedDuringMetadataCommit = false;
        try
        {
            await copyStarted.Task;
            invalidationTask = Task.Run(() => application.Authenticate("administrator", "wrong"));
            invalidationBlockedDuringCopy = SpinWait.SpinUntil(
                () => invalidationTask.Status == TaskStatus.Running,
                TimeSpan.FromSeconds(1))
                && !invalidationTask.IsCompleted;
            releaseCopy.SetResult(null);

            await metadataCommitStarted.Task;
            invalidationBlockedDuringMetadataCommit = !invalidationTask.IsCompleted;
            releaseMetadataCommit.SetResult(null);
            await restoreTask;
        }
        finally
        {
            releaseCopy.TrySetResult(null);
            releaseMetadataCommit.TrySetResult(null);
        }

        Assert.IsTrue(invalidationBlockedDuringCopy);
        Assert.IsTrue(invalidationBlockedDuringMetadataCommit);
        Assert.IsNotNull(invalidationTask);
        Assert.IsNull(await invalidationTask);
        Assert.AreEqual(1, copyCount);
        Assert.AreEqual(1, stores.Domains.Items.Count);
        Assert.AreEqual("restored", await File.ReadAllTextAsync(fixture.RestoredFilePath));
        Assert.IsFalse(File.Exists(fixture.OriginalFilePath));
        Assert.AreEqual(
            unchecked((int)0x80070005),
            Assert.ThrowsExactly<COMException>(() => _ = backup.ContainsDomains).ErrorCode);
        backup.CleanupArchiveBinding();
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
        using var binding = BackupArchiveBinding.TryCreate(fixture.ArchivePath);
        Assert.IsNotNull(binding);
        var backup = Backup.CreateAuthorized(
            6,
            binding.ArchivePath,
            archiveIdentity: binding.Identity,
            archiveBinding: binding,
            rawDataBackupIdentity: binding.RawDataBackupIdentity);
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
        public RecordingSettingsRestoreStore Settings { get; } = new();
        public RecordingSecurityRangeStore SecurityRanges { get; } = new();
        public RecordingTcpIpPortStore TcpIpPorts { get; } = new();
        public RecordingBlockedAttachmentStore BlockedAttachments { get; } = new();
        public RecordingPublicFolderRestoreStore PublicFolders { get; } = new();
        public RecordingPublicMessageRestoreStore PublicMessages { get; } = new();
        public RecordingPublicFolderPermissionRestoreStore PublicPermissions { get; } = new();
        public RecordingGroupStore Groups { get; } = new();
        public RecordingGroupMemberStore GroupMembers { get; } = new();
        public List<string> Events { get; set; } = [];
        public bool FailAliasInsert { get; init; }
        public bool FailDistributionListInsertAfterFirst { get; init; }
        public bool FailGroupMemberInsert { get; init; }

        public MetadataBackupRestoreExecutor CreateExecutor(
            string dataDirectory,
            Func<CancellationToken, ValueTask>? reinitialize = null,
            bool requireReinitialize = false,
            IBackupRestoreMetadataTransactionFactory? metadataTransactionFactory = null,
            bool requireSqlTransaction = false)
        {
            DistributionLists.FailInsertAfterFirst = FailDistributionListInsertAfterFirst;
            GroupMembers.FailInsert = FailGroupMemberInsert;
            return new(
                Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                dataDirectory,
                Domains,
                Accounts,
                FailAliasInsert ? new FailingAliasStore(Aliases) : Aliases,
                DistributionLists,
                Recipients,
                dataDirectoryRuntime: new BackupRestoreDataDirectoryRuntime(
                    Path.Combine(AppContext.BaseDirectory, "7za.exe"),
                    filesystemMutation: new DeterministicFilesystemMutation()),
                metadataTransactionFactory: metadataTransactionFactory,
                requireSqlTransaction: requireSqlTransaction,
                folderRestoreStore: PublicFolders,
                messageRestoreStore: PublicMessages,
                groupStore: Groups,
                groupMemberStore: GroupMembers,
                reinitialize: reinitialize,
                requireReinitialize: requireReinitialize);
        }
    }

    private sealed class NoopLease : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static Application CreateAuthenticatedApplication(IBackupArchiveMetadataReader reader)
    {
        var application = new Application(
            new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"),
            reader);

        Assert.IsNotNull(application.Authenticate("administrator", "secret"));
        return application;
    }

    private sealed class RecordingBackupArchiveMetadataReader(int options) : IBackupArchiveMetadataReader
    {
        public int ReadContainsOptions(string archivePath) => options;
    }

    private sealed class GatedDomainStore(
        IDomainAdministrationStore inner,
        TaskCompletionSource<object?> readStarted,
        TaskCompletionSource<object?> releaseRead) : IDomainAdministrationStore
    {
        public async ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken)
        {
            readStarted.TrySetResult(null);
            await releaseRead.Task.WaitAsync(cancellationToken);
            return await inner.GetDomainsAsync(cancellationToken);
        }

        public ValueTask<int> InsertDomainAsync(
            DomainAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            inner.InsertDomainAsync(snapshot, cancellationToken);

        public ValueTask<bool> DeleteDomainByIdAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            inner.DeleteDomainByIdAsync(domainId, cancellationToken);
    }

    private sealed class GatedRecipientStore(
        IDistributionListRecipientAdministrationStore inner,
        TaskCompletionSource<object?> insertStarted,
        TaskCompletionSource<object?> releaseInsert) : IDistributionListRecipientAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken) =>
            inner.GetRecipientsAsync(distributionListId, cancellationToken);

        public async ValueTask<int> InsertDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            insertStarted.TrySetResult(null);
            await releaseInsert.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await inner
                .InsertDistributionListRecipientAsync(snapshot, cancellationToken)
                .ConfigureAwait(false);
        }

        public ValueTask<bool> DeleteDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            inner.DeleteDistributionListRecipientAsync(snapshot, cancellationToken);
    }

    private sealed class RecordingMetadataTransactionFactory(RecordingStores stores) : IBackupRestoreMetadataTransactionFactory
    {
        public int BeginCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int SecurityRangeDeleteCount { get; private set; }
        public int TcpIpPortDeleteCount { get; private set; }
        public int BlockedAttachmentDeleteCount { get; private set; }
        public bool FailDelete { get; set; }
        public RecordingMetadataTransaction? LastTransaction { get; private set; }

        public ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            LastTransaction = new RecordingMetadataTransaction(stores, this);
            return ValueTask.FromResult<IBackupRestoreMetadataTransaction>(
                LastTransaction);
        }

        public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken)
        {
            DeleteCount++;
            if (FailDelete)
            {
                throw new InvalidOperationException("Injected domain cleanup failure.");
            }

            stores.Events.Add("delete-all-domains");
            stores.Domains.Items.Clear();
            stores.Accounts.Items.Clear();
            stores.Aliases.Items.Clear();
            stores.DistributionLists.Items.Clear();
            stores.Recipients.Items.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAllSecurityRangesForRestoreAsync(CancellationToken cancellationToken)
        {
            SecurityRangeDeleteCount++;
            stores.SecurityRanges.Items.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAllTcpIpPortsForRestoreAsync(CancellationToken cancellationToken)
        {
            TcpIpPortDeleteCount++;
            stores.TcpIpPorts.Items.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAllBlockedAttachmentsForRestoreAsync(CancellationToken cancellationToken)
        {
            BlockedAttachmentDeleteCount++;
            stores.BlockedAttachments.Items.Clear();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CommitGatedMetadataTransactionFactory(
        RecordingStores stores,
        TaskCompletionSource<object?> commitStarted,
        TaskCompletionSource<object?> releaseCommit) : IBackupRestoreMetadataTransactionFactory
    {
        public ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IBackupRestoreMetadataTransaction>(
                new CommitGatedMetadataTransaction(stores, commitStarted, releaseCommit));
    }

    private sealed class CommitGatedMetadataTransaction(
        RecordingStores stores,
        TaskCompletionSource<object?> commitStarted,
        TaskCompletionSource<object?> releaseCommit) : IBackupRestoreMetadataTransaction
    {
        public IDomainAdministrationStore DomainStore => stores.Domains;
        public IAccountAdministrationStore AccountStore => stores.Accounts;
        public IAliasAdministrationStore AliasStore => stores.Aliases;
        public IDistributionListAdministrationStore DistributionListStore => stores.DistributionLists;
        public IDistributionListRecipientAdministrationStore RecipientStore => stores.Recipients;
        public ISettingsRestoreAdministrationStore SettingsStore => stores.Settings;
        public IImapFolderAdministrationRestoreStore FolderRestoreStore => stores.PublicFolders;
        public IMessageAdministrationRestoreStore MessageRestoreStore => stores.PublicMessages;
        public IImapFolderPermissionAdministrationRestoreStore FolderPermissionRestoreStore => stores.PublicPermissions;
        public IGroupAdministrationStore GroupStore => stores.Groups;
        public IGroupMemberAdministrationStore GroupMemberStore => stores.GroupMembers;
        public ISecurityRangeAdministrationStore SecurityRangeStore => stores.SecurityRanges;
        public ITcpIpPortAdministrationStore TcpIpPortStore => stores.TcpIpPorts;
        public IBlockedAttachmentAdministrationStore BlockedAttachmentStore => stores.BlockedAttachments;

        public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DeleteAllGroupsForRestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DeleteAllTcpIpPortsForRestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DeleteAllBlockedAttachmentsForRestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public async ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            commitStarted.TrySetResult(null);
            await releaseCommit.Task.WaitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingMetadataTransaction(
        RecordingStores stores,
        RecordingMetadataTransactionFactory factory) : IBackupRestoreMetadataTransaction
    {
        public IDomainAdministrationStore DomainStore => stores.Domains;
        public IAccountAdministrationStore AccountStore => stores.Accounts;
        public IAliasAdministrationStore AliasStore => stores.Aliases;
        public IDistributionListAdministrationStore DistributionListStore => stores.DistributionLists;
        public IDistributionListRecipientAdministrationStore RecipientStore => stores.Recipients;
        public ISettingsRestoreAdministrationStore SettingsStore => stores.Settings;
        public IImapFolderAdministrationRestoreStore FolderRestoreStore => stores.PublicFolders;
        public IMessageAdministrationRestoreStore MessageRestoreStore => stores.PublicMessages;
        public IImapFolderPermissionAdministrationRestoreStore FolderPermissionRestoreStore => stores.PublicPermissions;
        public IGroupAdministrationStore GroupStore => stores.Groups;
        public IGroupMemberAdministrationStore GroupMemberStore => stores.GroupMembers;
        public ISecurityRangeAdministrationStore SecurityRangeStore => stores.SecurityRanges;
        public ITcpIpPortAdministrationStore TcpIpPortStore => stores.TcpIpPorts;
        public IBlockedAttachmentAdministrationStore BlockedAttachmentStore => stores.BlockedAttachments;

        public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
            factory.DeleteAllDomainsForRestoreAsync(cancellationToken);

        public ValueTask DeleteAllPublicFoldersForRestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DeleteAllGroupsForRestoreAsync(CancellationToken cancellationToken)
        {
            stores.Events.Add("delete-all-groups");
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAllSecurityRangesForRestoreAsync(CancellationToken cancellationToken) =>
            factory.DeleteAllSecurityRangesForRestoreAsync(cancellationToken);

        public ValueTask DeleteAllTcpIpPortsForRestoreAsync(CancellationToken cancellationToken)
            => factory.DeleteAllTcpIpPortsForRestoreAsync(cancellationToken);

        public ValueTask DeleteAllBlockedAttachmentsForRestoreAsync(CancellationToken cancellationToken)
            => factory.DeleteAllBlockedAttachmentsForRestoreAsync(cancellationToken);

        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            if (CommitCount == 0)
            {
                RolledBack = true;
                stores.Groups.Inserted.Clear();
                stores.GroupMembers.Inserted.Clear();
                stores.PublicPermissions.Inserted.Clear();
                stores.SecurityRanges.Items.Clear();
                stores.TcpIpPorts.Items.Clear();
                stores.BlockedAttachments.Items.Clear();
                stores.Settings.Properties.Clear();
            }

            return ValueTask.CompletedTask;
        }

        public bool Disposed { get; private set; }
        public bool RolledBack { get; private set; }
        public int CommitCount { get; private set; }
    }

    private sealed class RecordingSettingsRestoreStore : ISettingsRestoreAdministrationStore
    {
        public List<BackupSettingsPropertySnapshot> Properties { get; } = [];
        public bool Fail { get; set; }
        public Action? OnRestore { get; set; }

        public ValueTask RestoreSettingsPropertiesAsync(
            IReadOnlyList<BackupSettingsPropertySnapshot> properties,
            CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Injected settings restore failure.");
            }

            OnRestore?.Invoke();
            Properties.AddRange(properties);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingSecurityRangeStore : ISecurityRangeAdministrationStore
    {
        public List<SecurityRangeAdministrationSnapshot> Items { get; } = [];
        public bool Fail { get; set; }
        public int InsertAttempts { get; private set; }

        public ValueTask<IReadOnlyList<SecurityRangeAdministrationSnapshot>> GetSecurityRangesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<SecurityRangeAdministrationSnapshot>>(Items);

        public ValueTask<int> InsertSecurityRangeAsync(
            SecurityRangeAdministrationSnapshot range,
            CancellationToken cancellationToken)
        {
            InsertAttempts++;
            if (Fail)
            {
                return ValueTask.FromException<int>(
                    new InvalidOperationException("Injected security-range restore failure."));
            }

            Items.Add(range);
            return ValueTask.FromResult(500 + InsertAttempts);
        }

        public ValueTask UpdateSecurityRangeAsync(
            SecurityRangeAdministrationSnapshot range,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DeleteSecurityRangeByIdAsync(
            int databaseId,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class RecordingTcpIpPortStore : ITcpIpPortAdministrationStore
    {
        public List<TcpIpPortAdministrationSnapshot> Items { get; } = [];
        public bool Fail { get; set; }
        public int InsertAttempts { get; private set; }

        public ValueTask<IReadOnlyList<TcpIpPortAdministrationSnapshot>> GetTcpIpPortsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<TcpIpPortAdministrationSnapshot>>(Items);

        public ValueTask<int> InsertTcpIpPortForRestoreAsync(
            TcpIpPortAdministrationSnapshot port,
            CancellationToken cancellationToken)
        {
            InsertAttempts++;
            if (Fail)
            {
                return ValueTask.FromException<int>(
                    new InvalidOperationException("Injected TCP/IP port restore failure."));
            }

            Items.Add(port with { Id = 700 + InsertAttempts });
            return ValueTask.FromResult(700 + InsertAttempts);
        }

        public ValueTask<int> InsertTcpIpPortAsync(
            TcpIpPortAdministrationSnapshot port,
            CancellationToken cancellationToken) =>
            InsertTcpIpPortForRestoreAsync(port, cancellationToken);

        public ValueTask DeleteTcpIpPortByIdAsync(
            int databaseId,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask UpdateTcpIpPortAsync(
            TcpIpPortAdministrationSnapshot port,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class RecordingBlockedAttachmentStore : IBlockedAttachmentAdministrationStore
    {
        public List<BlockedAttachmentAdministrationSnapshot> Items { get; } = [];
        public bool Fail { get; set; }
        public int InsertAttempts { get; private set; }

        public ValueTask<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>> GetBlockedAttachmentsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>>(Items);

        public ValueTask<int> InsertBlockedAttachmentForRestoreAsync(
            BlockedAttachmentAdministrationSnapshot attachment,
            CancellationToken cancellationToken)
        {
            InsertAttempts++;
            if (Fail)
            {
                return ValueTask.FromException<int>(
                    new InvalidOperationException("Injected blocked-attachment restore failure."));
            }

            Items.Add(attachment with { Id = 800 + InsertAttempts });
            return ValueTask.FromResult(800 + InsertAttempts);
        }

        public ValueTask<int> InsertBlockedAttachmentAsync(
            BlockedAttachmentAdministrationSnapshot attachment,
            CancellationToken cancellationToken) =>
            InsertBlockedAttachmentForRestoreAsync(attachment, cancellationToken);

        public ValueTask UpdateBlockedAttachmentAsync(
            BlockedAttachmentAdministrationSnapshot attachment,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DeleteBlockedAttachmentByIdAsync(
            int databaseId,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class RecordingPublicFolderRestoreStore : IImapFolderAdministrationRestoreStore
    {
        private int _nextId = 500;

        public List<ImapFolderAdministrationSnapshot> Inserted { get; } = [];

        public ValueTask<ImapFolderAdministrationSnapshot> InsertFolderForRestoreAsync(
            ImapFolderAdministrationSnapshot folder,
            CancellationToken cancellationToken)
        {
            var inserted = folder with { Id = _nextId++ };
            Inserted.Add(inserted);
            return ValueTask.FromResult(inserted);
        }
    }

    private sealed class RecordingPublicMessageRestoreStore : IMessageAdministrationRestoreStore
    {
        private long _nextId = 700;

        public List<(int AccountId, int FolderId, MessageAdministrationSnapshot Snapshot)> Inserted { get; } = [];

        public ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
            int accountId,
            int folderId,
            MessageAdministrationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            Inserted.Add((accountId, folderId, snapshot));
            return ValueTask.FromResult(new MessageAdministrationInsertResult(_nextId++, snapshot.Uid, snapshot.State));
        }
    }

    private sealed class RecordingPublicFolderPermissionRestoreStore
        : IImapFolderPermissionAdministrationRestoreStore
    {
        public List<(int FolderId, int PermissionType, int PermissionGroupId, int PermissionAccountId, int Value)> Inserted { get; } = [];

        public ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionForRestoreAsync(
            int folderId,
            int permissionType,
            int permissionGroupId,
            int permissionAccountId,
            int value,
            CancellationToken cancellationToken)
        {
            Inserted.Add((folderId, permissionType, permissionGroupId, permissionAccountId, value));
            return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(
                new ImapFolderPermissionAdministrationSnapshot(
                    Inserted.Count,
                    folderId,
                    permissionType,
                    permissionGroupId,
                    permissionAccountId,
                    value));
        }
    }

    private sealed class RecordingGroupStore : IGroupAdministrationStore
    {
        public List<GroupAdministrationSnapshot> Inserted { get; } = [];

        public ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupAdministrationSnapshot>>(
                Inserted.Count == 0
                    ? new[] { new GroupAdministrationSnapshot(77, "Editors") }
                    : Inserted.ToArray());

        public ValueTask<int> InsertGroupAsync(
            GroupAdministrationSnapshot group,
            CancellationToken cancellationToken)
        {
            var id = 77 + Inserted.Count;
            Inserted.Add(group with { Id = id });
            return ValueTask.FromResult(id);
        }
    }

    private sealed class RecordingGroupMemberStore : IGroupMemberAdministrationStore
    {
        public List<GroupMemberAdministrationSnapshot> Inserted { get; } = [];
        public bool FailInsert { get; set; }
        public int InsertAttempts { get; private set; }

        public ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
            int groupId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupMemberAdministrationSnapshot>>(
                Inserted.Where(member => member.GroupId == groupId).ToArray());

        public ValueTask<int> InsertGroupMemberAsync(
            GroupMemberAdministrationSnapshot member,
            CancellationToken cancellationToken)
        {
            InsertAttempts++;
            if (FailInsert)
            {
                return ValueTask.FromException<int>(
                    new InvalidOperationException("Injected group-member restore failure."));
            }

            var id = 88 + Inserted.Count;
            Inserted.Add(member with { Id = id });
            return ValueTask.FromResult(id);
        }
    }

    private sealed class RecordingDomainStore : IDomainAdministrationStore
    {
        public List<DomainAdministrationSnapshot> Items { get; } = [];
        public List<int> Deleted { get; } = [];
        public Action<string>? EventSink { get; set; }

        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>(Items.ToArray());

        public ValueTask<int> InsertDomainAsync(DomainAdministrationSnapshot snapshot, CancellationToken cancellationToken)
        {
            EventSink?.Invoke("insert-domain");
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
        public List<(string Password, int PasswordEncryption)> RestoredCredentials { get; } = [];

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

        public ValueTask<int> InsertAccountForRestoreAsync(
            int domainId,
            AccountAdministrationSnapshot snapshot,
            string password,
            int passwordEncryption,
            CancellationToken cancellationToken)
        {
            RestoredCredentials.Add((password, passwordEncryption));
            return InsertAccountAsync(domainId, snapshot, password, cancellationToken);
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

    private sealed class DeterministicFilesystemMutation : IBackupRestoreDataDirectoryMutation
    {
        public void MoveDirectory(string sourcePath, string destinationPath) =>
            Directory.Move(sourcePath, destinationPath);
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

        public static async Task<ArchiveFixture> CreateNonDbAsync(
            bool compressed,
            string? customXml = null)
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
            var xml = customXml ?? ArchiveXml.Replace(
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
