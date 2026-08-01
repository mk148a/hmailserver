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
    public async Task InspectAsync_RequiresCompressedMessageFilesAtLegacyGuidBucketPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string filename = "{0123456789abcdef0123456789abcdef}.eml";
        var metadata = CreateMessageBackupMetadata(filename, mode: 14);
        using var fixture = await ArchiveFixture.CreateAsync(
            metadata,
            includeNestedDataBackup: false,
            dataBackupFiles: new[] { $"example.com/alice/01/{filename}" });

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.MessageFilesValidated);
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMissingCompressedMessageFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string filename = "{0123456789abcdef0123456789abcdef}.eml";
        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata(filename, mode: 14),
            includeNestedDataBackup: false,
            dataBackupFiles: new[] { "example.com/alice/01/other.eml" });

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "compressed message file is missing");
    }

    [TestMethod]
    public async Task InspectAsync_ValidatesRawMessageFileCorrespondence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string filename = "message.eml";
        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata(filename, mode: 6, format: "Raw"),
            includeNestedDataBackup: false,
            createRawSibling: false,
            rawDataBackupFiles: new[] { "example.com/alice/es/message.eml" });

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.MessageFilesValidated);
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMissingRawMessageFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata("message.eml", mode: 6, format: "Raw"),
            includeNestedDataBackup: false,
            createRawSibling: false,
            rawDataBackupFiles: new[] { "example.com/alice/es/other.eml" });

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "raw message file is missing");
    }

    [TestMethod]
    public async Task InspectAsync_RejectsMessageFilenameTraversal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata("..\\escape.eml", mode: 14),
            includeNestedDataBackup: false,
            dataBackupFiles: new[] { "example.com/alice/es/escape.eml" });

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "safe file name");
    }

    [TestMethod]
    [DataRow("message.eml.")]
    [DataRow("message.eml ")]
    [DataRow("CON.txt")]
    [DataRow("NUL")]
    [DataRow("name:stream.eml")]
    public async Task InspectAsync_RejectsWindowsUnsafeMessageFilenames(string filename)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata(filename, mode: 14),
            includeNestedDataBackup: false,
            dataBackupFiles: new[] { "example.com/alice/es/other.eml" });

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid);
        StringAssert.Contains(evidence.FailureReason!, "safe file name");
    }

    [TestMethod]
    public async Task InspectAsync_AllowsDbOnlyMessageMetadataWithoutPhysicalFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string filename = "{0123456789abcdef0123456789abcdef}.eml";
        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata(filename, mode: 14),
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None,
            backupMessagesDbOnly: true);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.MessageFilesValidated);
    }

    [TestMethod]
    public async Task InspectAsync_AllowsDbOnlyRawMessageMetadataWithoutPhysicalFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata("message.eml", mode: 6, format: "Raw"),
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None,
            backupMessagesDbOnly: true);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
        Assert.IsTrue(evidence.MessageFilesValidated);
    }

    [TestMethod]
    public async Task RevalidateAsync_RejectsDeletedRawMessageFileUsingFreshEvidence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata("message.eml", mode: 6, format: "Raw"),
            includeNestedDataBackup: false,
            createRawSibling: false,
            rawDataBackupFiles: new[] { "example.com/alice/es/message.eml" });
        var initialEvidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None);
        Assert.IsTrue(initialEvidence.IsValid, initialEvidence.FailureReason);

        var targetPath = Directory.CreateDirectory(
            Path.Combine(fixture.DirectoryPath, "target-data")).FullName;
        var rollbackPath = Path.Combine(fixture.DirectoryPath, "rollback", "state.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackPath)!);
        var initialPlan = BackupRestoreContainmentPreflight.Plan(
            initialEvidence,
            targetPath,
            rollbackPath);
        Assert.IsTrue(initialPlan.IsSafe, initialPlan.FailureReason);

        File.Delete(Path.Combine(
            initialEvidence.RawDataBackupPath!,
            "example.com",
            "alice",
            "es",
            "message.eml"));

        var revalidatedPlan = await BackupRestoreContainmentPreflight.RevalidateAsync(
            initialPlan,
            initialEvidence,
            fixture.Runtime,
            CancellationToken.None);

        Assert.IsFalse(revalidatedPlan.IsSafe);
        StringAssert.Contains(revalidatedPlan.FailureReason!, "raw message file is missing");
        Assert.IsTrue(Directory.Exists(targetPath));
        Assert.IsFalse(File.Exists(rollbackPath));
    }

    [TestMethod]
    public async Task RevalidateAsync_RejectsChangedCompressedMessageGraphAtSameArchivePath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string filename = "{0123456789abcdef0123456789abcdef}.eml";
        using var fixture = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata(filename, mode: 14),
            includeNestedDataBackup: false,
            dataBackupFiles: new[] { $"example.com/alice/01/{filename}" });
        var initialEvidence = await fixture.Runtime.InspectAsync(
            fixture.ArchivePath,
            CancellationToken.None);
        Assert.IsTrue(initialEvidence.IsValid, initialEvidence.FailureReason);

        var targetPath = Directory.CreateDirectory(
            Path.Combine(fixture.DirectoryPath, "target-data")).FullName;
        var rollbackPath = Path.Combine(fixture.DirectoryPath, "rollback", "state.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackPath)!);
        var initialPlan = BackupRestoreContainmentPreflight.Plan(
            initialEvidence,
            targetPath,
            rollbackPath);
        Assert.IsTrue(initialPlan.IsSafe, initialPlan.FailureReason);

        using var replacement = await ArchiveFixture.CreateAsync(
            CreateMessageBackupMetadata(filename, mode: 14),
            includeNestedDataBackup: false,
            dataBackupFiles: new[] { "example.com/alice/01/other.eml" });
        File.Copy(replacement.ArchivePath, fixture.ArchivePath, overwrite: true);

        var revalidatedPlan = await BackupRestoreContainmentPreflight.RevalidateAsync(
            initialPlan,
            initialEvidence,
            fixture.Runtime,
            CancellationToken.None);

        Assert.IsFalse(revalidatedPlan.IsSafe);
        StringAssert.Contains(revalidatedPlan.FailureReason!, "compressed message file is missing");
        Assert.IsTrue(Directory.Exists(targetPath));
        Assert.IsFalse(File.Exists(rollbackPath));
    }

    private static string CreateMessageBackupMetadata(string filename, int mode, string format = "7z")
    {
        var folderName = string.Equals(format, "Raw", StringComparison.OrdinalIgnoreCase)
            ? " FolderName=\"DataBackup\""
            : string.Empty;
        return $"<Backup><BackupInformation Mode=\"{mode}\"><DataFiles Format=\"{format}\"{folderName} /></BackupInformation><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\" Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\"><Messages><Message CreateTime=\"2026-08-01 00:00:00\" Filename=\"{filename}\" FromAddress=\"sender@example.com\" State=\"1\" Size=\"10\" NoOfRetries=\"0\" Flags=\"0\" ID=\"1\" UID=\"1\" /></Messages></Folder></Folders></Account></Accounts></Domain></Domains></Backup>";
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
    [DataRow("example.com", "example.com")]
    [DataRow("example.com", "EXAMPLE.COM")]
    [DataRow("example.com", "example.com ")]
    public async Task InspectAsync_RejectsDuplicateDomainNameSiblings(string firstName, string secondName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            $"<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"{firstName}\" /><Domain Name=\"{secondName}\" /></Domains></Backup>",
            includeNestedDataBackup: false);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, evidence.FailureReason);
        StringAssert.Contains(evidence.FailureReason!, "duplicate Domain Name");
        CollectionAssert.AreEqual(before, fixture.Snapshot());
    }

    [TestMethod]
    [DataRow("alice@example.com", "alice@example.com")]
    [DataRow("alice@example.com", "ALICE@EXAMPLE.COM")]
    [DataRow("alice@example.com", "alice@example.com ")]
    public async Task InspectAsync_RejectsDuplicateAccountNameSiblings(string firstName, string secondName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            $"<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"{firstName}\" /><Account Name=\"{secondName}\" /></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, evidence.FailureReason);
        StringAssert.Contains(evidence.FailureReason!, "duplicate Account Name");
        CollectionAssert.AreEqual(before, fixture.Snapshot());
    }

    [TestMethod]
    public async Task InspectAsync_RejectsDuplicateAccountNameAcrossDomains()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\" /></Accounts></Domain><Domain Name=\"other.example\"><Accounts><Account Name=\"alice@example.com\" /></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, evidence.FailureReason);
        StringAssert.Contains(evidence.FailureReason!, "duplicate Account Name");
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
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><FetchAccounts><FetchAccount Name=\"remote\"><FetchAccountUIDs><UID UID=\"message-1\" Date=\"2026-08-01\" /></FetchAccountUIDs></FetchAccount></FetchAccounts><Rules><Rule Name=\"rule\"><RuleCriterias><Criteria MatchString=\"alice\" FieldType=\"1\" MatchType=\"2\" HeaderField=\"Subject\" UsePredefinedField=\"1\" /></RuleCriterias><RuleActions><Action Type=\"1\" Subject=\"subject\" Body=\"body\" FromAddress=\"from@example.com\" FromName=\"Sender\" IMAPFolder=\"Inbox\" FileName=\"reply.eml\" To=\"to@example.com\" ScriptFunction=\"OnAcceptMessage\" SortOrder=\"1\" Header=\"X-Test\" Value=\"value\" RouteID=\"0\" AbortSpamFlagged=\"0\" /></RuleActions></Rule></Rules><Folders><Folder Name=\"Inbox\" Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\"><Messages><Message CreateTime=\"2026-08-01 00:00:00\" Filename=\"message.eml\" FromAddress=\"sender@example.com\" State=\"1\" Size=\"10\" NoOfRetries=\"0\" Flags=\"0\" ID=\"1\" UID=\"1\" /></Messages><Folders><Folder Name=\"Nested\" Subscribed=\"0\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"0\"><Messages /></Folder></Folders></Folder></Folders></Account></Accounts></Domain></Domains></Backup>",
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
    public async Task InspectAsync_AcceptsExplicitlyEmptyFolderChildContainers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\" Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\"><Messages /><Folders /></Folder></Folders></Account></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    [DataRow("Inbox", "INBOX")]
    [DataRow("Inbox", "Inbox ")]
    public async Task InspectAsync_RejectsDuplicateFolderNamesWithinOneParent(string firstName, string secondName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            $"<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"{firstName}\" Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\" /><Folder Name=\"{secondName}\" Subscribed=\"0\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"0\" /></Folders></Account></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);
        var before = fixture.Snapshot();

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsFalse(evidence.IsValid, evidence.FailureReason);
        StringAssert.Contains(evidence.FailureReason!, "duplicate Folder Name");
        CollectionAssert.AreEqual(before, fixture.Snapshot());
    }

    [TestMethod]
    public async Task InspectAsync_AcceptsSameFolderNameUnderDifferentParents()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = await ArchiveFixture.CreateAsync(
            "<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\" Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\"><Folders><Folder Name=\"Inbox\" Subscribed=\"0\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"0\" /></Folders></Folder></Folders></Account></Accounts></Domain></Domains></Backup>",
            includeNestedDataBackup: false);

        var evidence = await fixture.Runtime.InspectAsync(fixture.ArchivePath, CancellationToken.None);

        Assert.IsTrue(evidence.IsValid, evidence.FailureReason);
    }

    [TestMethod]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Messages /></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\"><Messages /><Messages /></Folder></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\"><Folders /><Folders /></Folder></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\"><Messages><Folder /></Messages></Folder></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\"><Folders><Message /></Folders></Folder></Folders></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMalformedFolderChildGraph(string metadataXml)
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
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\"><Messages><Message Filename=\"message.eml\" FromAddress=\"sender@example.com\" State=\"1\" Size=\"10\" NoOfRetries=\"0\" Flags=\"0\" ID=\"1\" UID=\"1\" /></Messages></Folder></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\"><Messages><Message CreateTime=\"2026-08-01 00:00:00\" Filename=\"message.eml\" FromAddress=\"sender@example.com\" State=\"1\" Size=\"10\" NoOfRetries=\"0\" Flags=\"0\" ID=\"1\" /></Messages></Folder></Folders></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMissingMessageAttributes(string metadataXml)
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
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\" /></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\" CreateTime=\"2026-08-01 00:00:00\" CurrentUID=\"1\" /></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\" Subscribed=\"1\" CurrentUID=\"1\" /></Folders></Account></Accounts></Domain></Domains></Backup>")]
    [DataRow("<Backup><BackupInformation Mode=\"2\" /><Domains><Domain Name=\"example.com\"><Accounts><Account Name=\"alice@example.com\"><Folders><Folder Name=\"Inbox\" Subscribed=\"1\" CreateTime=\"2026-08-01 00:00:00\" /></Folders></Account></Accounts></Domain></Domains></Backup>")]
    public async Task InspectAsync_RejectsMissingFolderAttributes(string metadataXml)
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
            bool createCompressedFile = false,
            IReadOnlyList<string>? dataBackupFiles = null,
            IReadOnlyList<string>? rawDataBackupFiles = null)
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

            foreach (var relativeFile in dataBackupFiles ?? Array.Empty<string>())
            {
                var messagePath = Path.Combine(
                    new[] { source, "DataBackup" }
                        .Concat(relativeFile.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
                        .ToArray());
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

            foreach (var relativeFile in rawDataBackupFiles ?? Array.Empty<string>())
            {
                var messagePath = Path.Combine(
                    new[] { directory, "DataBackup" }
                        .Concat(relativeFile.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
                        .ToArray());
                Directory.CreateDirectory(Path.GetDirectoryName(messagePath)!);
                File.WriteAllText(messagePath, "message");
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

            if (includeNestedDataBackup || createCompressedFile || (dataBackupFiles?.Count > 0))
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
