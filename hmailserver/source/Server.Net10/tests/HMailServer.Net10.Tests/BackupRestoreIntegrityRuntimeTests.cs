using System.Diagnostics;
using System.Text;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreIntegrityRuntimeTests
{
    [TestMethod]
    public async Task InspectAsync_AcceptsCompressedNestedDataBackupWithoutMutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"14\"><DataFiles Format=\"7z\" Size=\"0\" /></BackupInformation><Domains><Domain Name=\"example.com\" /></Domains></Backup>",
            includeNestedDataBackup: true);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.ArchiveTestPassed);
        Assert.IsTrue(evidence.MetadataXmlValid);
        Assert.AreEqual(14, evidence.BackupOptions);
        Assert.AreEqual("7z", evidence.DataFilesFormat);
        Assert.IsTrue(
            evidence.ArchiveEntries.Any(static entry =>
                string.Equals(
                    entry.Replace('\\', '/'),
                    "DataBackup/accounts/alice/message.eml",
                    StringComparison.OrdinalIgnoreCase)));
        CollectionAssert.AreEqual(before, fixture.Snapshot());
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsLegacyDomainAccountGraphWithoutMutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DomainAliases><DomainAlias Name=\"alias.example.com\" /></DomainAliases><Accounts><Account Name=\"alice@example.com\" /></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.AreEqual(2, evidence.BackupOptions);
        CollectionAssert.AreEqual(before, fixture.Snapshot());
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsPopulatedDomainChildContainersAndUnknownDomainChild()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DomainAliases><DomainAlias Name=\"alias.example.com\" /></DomainAliases><Aliases><Alias Name=\"info@example.com\" Value=\"alice@example.com\" Active=\"1\" /></Aliases><DistributionLists><DistributionList Name=\"list@example.com\" Active=\"1\" RequiresAuth=\"0\" RequiresAuthAddress=\"\" ListMode=\"0\" /></DistributionLists><LegacyDomainChild /></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsExplicitlyEmptyDomainChildContainers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DomainAliases /><Aliases /><DistributionLists /></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsAccountChildContainersAndNestedFolders()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs><UID UID=\"message-1\" Date=\"2026-08-01\" /></FetchAccountUIDs></FetchAccount></FetchAccounts><Rules><Rule Name=\"rule\"><RuleCriterias><Criteria MatchString=\"alice\" FieldType=\"1\" MatchType=\"2\" HeaderField=\"Subject\" UsePredefinedField=\"1\" /></RuleCriterias><RuleActions><Action Type=\"1\" Subject=\"subject\" Body=\"body\" FromAddress=\"from@example.com\" FromName=\"Sender\" IMAPFolder=\"Inbox\" FileName=\"reply.eml\" To=\"to@example.com\" ScriptFunction=\"OnAcceptMessage\" SortOrder=\"1\" Header=\"X-Test\" Value=\"value\" RouteID=\"0\" AbortSpamFlagged=\"0\" /></RuleActions></Rule></Rules><Folders><Folder Name=\"Inbox\"><Folders><Folder Name=\"Nested\" /></Folders></Folder></Folders></Account></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsExplicitlyEmptyAccountChildContainers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs /></FetchAccount></FetchAccounts><Rules /><Folders /></Account></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><FetchAccounts /></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts /><FetchAccounts /></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules /><Rules /></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders /><Folders /></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMisplacedOrDuplicateAccountChildContainers(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><Rule /></FetchAccounts></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><FetchAccount /></Rules></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Alias /></Folders></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsWrongAccountChildContainerChildren(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsExplicitlyEmptyRuleChildContainers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleCriterias /><RuleActions /></Rule></Rules></Account></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><RuleCriterias /></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleCriterias /><RuleCriterias /></Rule></Rules></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleActions /><RuleActions /></Rule></Rules></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleCriterias><Action /></RuleCriterias></Rule></Rules></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleActions><Criteria /></RuleActions></Rule></Rules></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMalformedRuleChildGraph(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleCriterias><Criteria FieldType=\"1\" MatchType=\"2\" HeaderField=\"Subject\" UsePredefinedField=\"1\" /></RuleCriterias></Rule></Rules></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Rules><Rule Name=\"rule\"><RuleActions><Action Type=\"1\" Subject=\"subject\" Body=\"body\" FromAddress=\"from@example.com\" FromName=\"Sender\" IMAPFolder=\"Inbox\" FileName=\"reply.eml\" To=\"to@example.com\" ScriptFunction=\"OnAcceptMessage\" SortOrder=\"1\" Header=\"X-Test\" Value=\"value\" RouteID=\"0\" /></RuleActions></Rule></Rules></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMissingRuleChildAttributes(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs /><FetchAccountUIDs /></FetchAccount></FetchAccounts></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs><Rule /></FetchAccountUIDs></FetchAccount></FetchAccounts></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccountUIDs /></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMalformedFetchAccountUIDGraph(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs><UID Date=\"2026-08-01\" /></FetchAccountUIDs></FetchAccount></FetchAccounts></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs><UID UID=\"message-1\" /></FetchAccountUIDs></FetchAccount></FetchAccounts></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMissingFetchAccountUIDAttributes(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><DomainAliases><DomainAlias Name=\"alias.example.com\" /></DomainAliases><Domains><Domain Name=\"example.com\" /></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DomainAliases /><DomainAliases /></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Aliases /><Aliases /></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists /><DistributionLists /></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMisplacedOrDuplicateDomainChildContainers(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DomainAliases><Alias /></DomainAliases></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Aliases><DomainAlias /></Aliases></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists><Alias /></DistributionLists></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsWrongDomainChildContainerChildren(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DomainAliases><DomainAlias /></DomainAliases></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Aliases><Alias Value=\"alice@example.com\" Active=\"1\" /></Aliases></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Aliases><Alias Name=\"info@example.com\" Active=\"1\" /></Aliases></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Aliases><Alias Name=\"info@example.com\" Value=\"alice@example.com\" /></Aliases></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists><DistributionList Active=\"1\" RequiresAuth=\"0\" RequiresAuthAddress=\"\" ListMode=\"0\" /></DistributionLists></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists><DistributionList Name=\"list@example.com\" RequiresAuth=\"0\" RequiresAuthAddress=\"\" ListMode=\"0\" /></DistributionLists></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists><DistributionList Name=\"list@example.com\" Active=\"1\" RequiresAuthAddress=\"\" ListMode=\"0\" /></DistributionLists></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists><DistributionList Name=\"list@example.com\" Active=\"1\" RequiresAuth=\"0\" ListMode=\"0\" /></DistributionLists></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><DistributionLists><DistributionList Name=\"list@example.com\" Active=\"1\" RequiresAuth=\"0\" RequiresAuthAddress=\"\" /></DistributionLists></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMissingDomainChildScalarAttributes(string metadataXml)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(metadataXml, includeNestedDataBackup: false);
        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, metadataXml);
        StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMalformedLegacyDomainAccountGraph()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var metadata = new[]
        {
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain><Accounts><Account Name=\"alice@example.com\" /></Accounts></Domain></Domains></Backup>",
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account /></Accounts></Domain></Domains></Backup>",
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Account Name=\"alice@example.com\" /></Domain></Domains></Backup>",
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Domain Name=\"nested.example.com\" /></Accounts></Domain></Domains></Backup>",
            "<Backup><BackupInformation Mode=\"2\" /><Accounts><Account Name=\"alice@example.com\" /></Accounts></Backup>"
        };

        foreach (var xml in metadata)
        {
            using var fixture = await ArchiveFixture.CreateAsync(xml, includeNestedDataBackup: false);
            var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

            Assert.IsFalse(evidence.IsValid, xml);
            StringAssert.Contains(evidence.FailureReason!, "domain/account graph");
        }
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsCompressedDbOnlyMessageMetadataWithoutDataBackup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"12\"><DataFiles Format=\"7z\" /></BackupInformation></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None,
            backupMessagesDbOnly: true);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.BackupMessagesDbOnly);
        Assert.AreEqual(12, evidence.BackupOptions);
        Assert.AreEqual("7z", evidence.DataFilesFormat);
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsRawDbOnlyMessageMetadataWithoutDataBackup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"4\"><DataFiles Format=\"Raw\" FolderName=\"DataBackup\" /></BackupInformation></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None,
            backupMessagesDbOnly: true);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.BackupMessagesDbOnly);
        Assert.AreEqual(4, evidence.BackupOptions);
        Assert.AreEqual("Raw", evidence.DataFilesFormat);
        Assert.IsNull(evidence.RawDataBackupPath);
    }

    [TestMethod]
    public async Task InspectAsync_RejectsCompressedMetadataWithoutDataBackup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"14\"><DataFiles Format=\"7z\" /></BackupInformation><Domains><Domain Name=\"example.com\" /></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "DataBackup");
    }

    [TestMethod]
    public async Task InspectAsync_RejectsModeAndDataFilesMismatches()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var metadata = new[]
        {
            (Xml: "<Backup><BackupInformation Mode=\"4\" /></Backup>", Failure: "BOMessages"),
            (Xml: "<Backup><BackupInformation Mode=\"2\"><DataFiles Format=\"Raw\" FolderName=\"DataBackup\" /></BackupInformation></Backup>", Failure: "BOMessages"),
            (Xml: "<Backup><BackupInformation Mode=\"12\"><DataFiles Format=\"Raw\" /></BackupInformation></Backup>", Failure: "compression")
        };

        foreach (var item in metadata)
        {
            using var fixture = await ArchiveFixture.CreateAsync(
                item.Xml,
                includeNestedDataBackup: false);
            var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

            Assert.IsFalse(evidence.IsValid, item.Xml);
            StringAssert.Contains(evidence.FailureReason!, item.Failure);
        }
    }

    [TestMethod]
    public async Task InspectAsync_RejectsCompressedDataBackupForDbOnlyMode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"12\"><DataFiles Format=\"7z\" /></BackupInformation></Backup>",
            includeNestedDataBackup: true);

        var evidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None,
            backupMessagesDbOnly: true);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "DB-only");
    }

    [TestMethod]
    public async Task InspectAsync_RejectsCompressedFileNamedDataBackup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"14\"><DataFiles Format=\"7z\" /></BackupInformation><Domains><Domain Name=\"example.com\" /></Domains></Backup>",
            includeNestedDataBackup: false,
            createCompressedFile: true);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "directory");
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsRawExistingSiblingWithoutMutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"6\"><DataFiles Format=\"Raw\" FolderName=\"DataBackup\" /></BackupInformation><Domains><Domain Name=\"example.com\" /></Domains></Backup>",
            includeNestedDataBackup: false,
            createRawSibling: true);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.AreEqual("Raw", evidence.DataFilesFormat);
        Assert.AreEqual(Path.Combine(fixture.DirectoryPath, "DataBackup"), evidence.RawDataBackupPath);
        CollectionAssert.AreEqual(before, fixture.Snapshot());
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMissingAndCorruptMetadataPayloads()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using (var missing = await ArchiveFixture.CreateAsync(metadataXml: null, includeNestedDataBackup: true))
        {
            var evidence = await missing.Runtime.InspectAsync(missing.ArchivePath, CancellationToken.None);

            Assert.IsFalse(evidence.IsValid);
            StringAssert.Contains(evidence.FailureReason!, "hMailServerBackup.xml");
        }

        using var corrupt = ArchiveFixture.CreateCorrupt();
        var corruptEvidence = await corrupt.Runtime.InspectAsync(corrupt.ArchivePath, CancellationToken.None);

        Assert.IsFalse(corruptEvidence.IsValid);
        Assert.IsFalse(corruptEvidence.ArchiveTestPassed);
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMalformedAndDtdMetadata()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var metadata = new[]
        {
            "<Backup><BackupInformation Mode=\"7\"></Backup>",
            "<!DOCTYPE Backup [<!ENTITY xxe SYSTEM \"file:///secret\">]><Backup><BackupInformation Mode=\"7\" />&xxe;</Backup>"
        };

        foreach (var xml in metadata)
        {
            using var fixture = await ArchiveFixture.CreateAsync(xml, includeNestedDataBackup: false);
            var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

            Assert.IsFalse(evidence.IsValid);
            Assert.IsTrue(evidence.MetadataPresent);
            Assert.IsFalse(evidence.MetadataXmlValid);
        }
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMissingAndFileShapedRawSibling()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using (var missing = await ArchiveFixture.CreateAsync(
                   "<Backup><BackupInformation Mode=\"4\"><DataFiles Format=\"Raw\" FolderName=\"DataBackup\" /></BackupInformation></Backup>",
                   includeNestedDataBackup: false))
        {
            var evidence = await missing.Runtime.InspectAsync(missing.ArchivePath, CancellationToken.None);
            Assert.IsFalse(evidence.IsValid);
        }

        using var fileShaped = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"4\"><DataFiles Format=\"Raw\" FolderName=\"DataBackup\" /></BackupInformation></Backup>",
            includeNestedDataBackup: false,
            createRawSibling: false,
            createRawFile: true);
        var fileEvidence = await fileShaped.Runtime.InspectAsync(fileShaped.ArchivePath, CancellationToken.None);

        Assert.IsFalse(fileEvidence.IsValid);
    }

    [TestMethod]
    public async Task InspectAsync_RejectsRawTraversalAndAbsoluteSiblingNames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var folderName in new[] { "..\\DataBackup", @"C:\DataBackup" })
        {
            using var fixture = await ArchiveFixture.CreateAsync(
                $"<Backup><BackupInformation Mode=\"4\"><DataFiles Format=\"Raw\" FolderName=\"{folderName}\" /></BackupInformation></Backup>",
                includeNestedDataBackup: false);
            var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

            Assert.IsFalse(evidence.IsValid);
        }
    }

    [TestMethod]
    public void ArchiveAndRawPathValidation_RejectsTraversalAndAbsoluteEntries()
    {
        Assert.IsTrue(BackupRestoreIntegrityRuntime.IsSafeArchiveEntryPath("DataBackup/accounts/alice/message.eml"));
        Assert.IsFalse(BackupRestoreIntegrityRuntime.IsSafeArchiveEntryPath("DataBackup/../escape"));
        Assert.IsFalse(BackupRestoreIntegrityRuntime.IsSafeArchiveEntryPath(@"C:\escape"));
        Assert.IsFalse(BackupRestoreIntegrityRuntime.IsSafeArchiveEntryPath(@"\\server\share\escape"));
    }

    private sealed class ArchiveFixture : IDisposable
    {
        private ArchiveFixture(string directoryPath, string archivePath, string sevenZipPath)
        {
            DirectoryPath = directoryPath;
            ArchivePath = archivePath;
            Runtime = new BackupRestoreIntegrityRuntime(sevenZipPath);
        }

        public string DirectoryPath { get; }
        public string ArchivePath { get; }
        public BackupRestoreIntegrityRuntime Runtime { get; }

        public static async Task<ArchiveFixture> CreateAsync(
            string? metadataXml,
            bool includeNestedDataBackup,
            bool createRawSibling = false,
            bool createRawFile = false,
            bool createCompressedFile = false)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"hmailserver-restore-{Guid.NewGuid():N}");
            var source = Path.Combine(directory, "source");
            Directory.CreateDirectory(source);
            var archivePath = Path.Combine(directory, "backup.7z");
            var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
            Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);

            if (metadataXml is not null)
            {
                File.WriteAllText(
                    Path.Combine(source, SevenZipBackupArchiveMetadataReader.MetadataEntryName),
                    metadataXml);
            }

            if (includeNestedDataBackup)
            {
                var messagePath = Path.Combine(source, "DataBackup", "accounts", "alice", "message.eml");
                Directory.CreateDirectory(Path.GetDirectoryName(messagePath)!);
                File.WriteAllText(messagePath, "message");
            }

            if (createRawSibling)
            {
                Directory.CreateDirectory(Path.Combine(directory, "DataBackup", "accounts"));
                File.WriteAllText(Path.Combine(directory, "DataBackup", "accounts", "message.eml"), "message");
            }
            else if (createRawFile)
            {
                File.WriteAllText(Path.Combine(directory, "DataBackup"), "not a directory");
            }

            if (createCompressedFile)
            {
                File.WriteAllText(Path.Combine(source, "DataBackup"), "not a directory");
            }

            var arguments = new List<string> { "a", archivePath };
            if (metadataXml is not null)
            {
                arguments.Add(SevenZipBackupArchiveMetadataReader.MetadataEntryName);
            }

            if (includeNestedDataBackup || createCompressedFile)
            {
                arguments.Add("DataBackup");
            }

            arguments.Add("-t7z");
            arguments.Add("-mx1");
            await RunSevenZipAsync(sevenZipPath, source, arguments);
            return new ArchiveFixture(directory, archivePath, sevenZipPath);
        }

        public static ArchiveFixture CreateCorrupt()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"hmailserver-restore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var archivePath = Path.Combine(directory, "backup.7z");
            File.WriteAllText(archivePath, "not a 7z archive");
            var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
            Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);
            return new ArchiveFixture(directory, archivePath, sevenZipPath);
        }

        public string[] Snapshot() => Directory
            .EnumerateFileSystemEntries(DirectoryPath, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static async Task RunSevenZipAsync(
            string executablePath,
            string workingDirectory,
            IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.AreEqual(0, process.ExitCode, error);
        }
    }
}
