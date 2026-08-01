using System.Diagnostics;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using System.Xml.Linq;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupArchiveRuntimeTests
{
    [TestMethod]
    public async Task CreatesLegacyMetadataArchiveAndRemovesTemporaryXml()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);

        try
        {
            var runtime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-B0",
                static () => new DateTime(2026, 7, 30, 4, 5, 6));
            var evidence = new BackupStartPlanEvidence(
                Destination: destination + "\\",
                BackupOptions: 8,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);

            await runtime.CreateAsync(evidence, CancellationToken.None);

            var archivePath = Path.Combine(destination, "HMBackup 2026-07-30 040506.7z");
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            Assert.IsFalse(File.Exists(Path.Combine(destination, "hMailServerBackup.xml")));

            var reader = new SevenZipBackupArchiveMetadataReader(sevenZipPath);
            Assert.AreEqual(8, reader.ReadContainsOptions(archivePath));
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "DataBackup")));
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [TestMethod]
    public async Task RejectsPayloadOptionsBeforeCreatingAnyFile()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);

        try
        {
            var runtime = new SevenZipBackupArchiveRuntime(
                Path.Combine(destination, "missing-7za.exe"),
                "10.0.0-B0");
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);

            await Assert.ThrowsExactlyAsync<NotSupportedException>(
                () => runtime.CreateAsync(evidence, CancellationToken.None).AsTask());

            CollectionAssert.AreEqual(
                Array.Empty<string>(),
                Directory.GetFiles(destination));
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreatesDbOnlyMessageArchiveWithLegacyDataFilesMetadata()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);

        try
        {
            var runtime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-B0",
                static () => new DateTime(2026, 7, 30, 4, 5, 8));
            await runtime.CreateAsync(
                new BackupStartPlanEvidence(
                    Destination: destination,
                    BackupOptions: BackupStartPlan.BackupMessagesFlag | BackupStartPlan.BackupCompressionFlag,
                    BackupMessagesDbOnly: true,
                    AllMessageFilesInDataDirectory: true,
                    DestinationExists: true),
                CancellationToken.None);

            var archivePath = Path.Combine(destination, "HMBackup 2026-07-30 040508.7z");
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "DataBackup")));

            var metadata = XDocument.Parse(await ReadMetadataXmlAsync(sevenZipPath, archivePath));
            var dataFiles = metadata.Root!.Element("BackupInformation")!.Element("DataFiles")!;
            Assert.AreEqual("7z", dataFiles.Attribute("Format")?.Value);
            Assert.AreEqual("0", dataFiles.Attribute("Size")?.Value);
            Assert.IsNull(dataFiles.Attribute("FolderName"));
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreatesCompressedMessageArchiveWithStagedDataDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);
        var sourceDirectory = Path.Combine(Path.GetTempPath(), $"hmailserver-data-{Guid.NewGuid():N}");
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "accounts", "alice"));
        File.WriteAllText(Path.Combine(sourceDirectory, "server.dat"), "root metadata");
        File.WriteAllText(
            Path.Combine(sourceDirectory, "accounts", "alice", "message.eml"),
            "message body");

        try
        {
            var runtime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-B0",
                static () => new DateTime(2026, 7, 30, 4, 5, 9),
                payloadProvider: static (_, _) => ValueTask.FromResult(
                    new BackupArchiveXmlPayload(
                        Settings: null,
                        Domains: Array.Empty<DomainAdministrationSnapshot>())),
                dataDirectory: sourceDirectory);
            await runtime.CreateAsync(
                new BackupStartPlanEvidence(
                    Destination: destination,
                    BackupOptions: BackupStartPlan.BackupDomainsFlag
                        | BackupStartPlan.BackupMessagesFlag
                        | BackupStartPlan.BackupCompressionFlag,
                    BackupMessagesDbOnly: false,
                    AllMessageFilesInDataDirectory: true,
                    DestinationExists: true),
                CancellationToken.None);

            var archivePath = Path.Combine(destination, "HMBackup 2026-07-30 040509.7z");
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "DataBackup")));
            Assert.IsTrue(File.Exists(Path.Combine(sourceDirectory, "server.dat")));

            var extracted = Path.Combine(destination, "extracted");
            await ExtractArchiveAsync(sevenZipPath, archivePath, extracted);
            Assert.IsTrue(
                File.Exists(Path.Combine(extracted, "DataBackup", "accounts", "alice", "message.eml")));
            Assert.IsFalse(File.Exists(Path.Combine(extracted, "DataBackup", "server.dat")));

            var metadata = XDocument.Parse(await ReadMetadataXmlAsync(sevenZipPath, archivePath));
            var dataFiles = metadata.Root!.Element("BackupInformation")!.Element("DataFiles")!;
            Assert.AreEqual("7z", dataFiles.Attribute("Format")?.Value);
            Assert.AreEqual("0", dataFiles.Attribute("Size")?.Value);
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
            Directory.Delete(sourceDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RejectsCompressedMessageBackupWithoutDataDirectory()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);

        try
        {
            var runtime = new SevenZipBackupArchiveRuntime(
                Path.Combine(destination, "missing-7za.exe"),
                "10.0.0-B0");
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: BackupStartPlan.BackupDomainsFlag
                    | BackupStartPlan.BackupMessagesFlag
                    | BackupStartPlan.BackupCompressionFlag,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => runtime.CreateAsync(evidence, CancellationToken.None).AsTask());
            CollectionAssert.AreEqual(Array.Empty<string>(), Directory.GetFiles(destination));
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [TestMethod]
    public void MetadataXmlWritesLegacyRawDataFilesMetadataWithoutCompression()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            BackupStartPlan.BackupMessagesFlag,
            "10.0.0-B0");

        var dataFiles = XDocument.Parse(xml).Root!.Element("BackupInformation")!.Element("DataFiles")!;
        Assert.AreEqual("Raw", dataFiles.Attribute("Format")?.Value);
        Assert.AreEqual("DataBackup", dataFiles.Attribute("FolderName")?.Value);
        Assert.IsNull(dataFiles.Attribute("Size"));
    }

    [TestMethod]
    public async Task RejectsPhysicalMessageBackupBeforeCreatingAnyFile()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);

        try
        {
            var runtime = new SevenZipBackupArchiveRuntime(
                Path.Combine(destination, "missing-7za.exe"),
                "10.0.0-B0");
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: BackupStartPlan.BackupMessagesFlag | BackupStartPlan.BackupCompressionFlag,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);

            await Assert.ThrowsExactlyAsync<NotSupportedException>(
                () => runtime.CreateAsync(evidence, CancellationToken.None).AsTask());

            CollectionAssert.AreEqual(
                Array.Empty<string>(),
                Directory.GetFiles(destination));
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [TestMethod]
    public void MetadataXmlPreservesLegacyModeAndVersionAttributes()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(8, "10.0.0-B0");

        StringAssert.Contains(xml, "<Backup>");
        StringAssert.Contains(xml, "<BackupInformation");
        StringAssert.Contains(xml, "Mode=\"8\"");
        StringAssert.Contains(xml, "Version=\"10.0.0-B0\"");
        StringAssert.Contains(xml, "</Backup>");
    }

    [TestMethod]
    public void MetadataXmlWritesSelectedSettingsAndDomainScalarsWithoutMessages()
    {
        var settings = new SettingsAdministrationSnapshot(
            HostName: "mail.example.test",
            WelcomeSmtp: "smtp",
            WelcomePop3: "pop3",
            WelcomeImap: "imap",
            BackupDestination: @"D:\MailBackup",
            BackupOptions: 3);
        var domain = new DomainAdministrationSnapshot(
            Id: 7,
            Name: "example.test",
            Active: true,
            Postmaster: "postmaster@example.test",
            MaxMessageSize: 50,
            PlusAddressingEnabled: true,
            PlusAddressingCharacter: "+",
            MaxSize: 1024,
            MaxAccountSize: 256,
            SignaturePlainText: "Regards",
            SignatureHtml: "<p>Regards</p>");

        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            3,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(settings, new[] { domain }));

        StringAssert.Contains(xml, "<Properties>");
        StringAssert.Contains(xml, "<backupdestination");
        StringAssert.Contains(xml, "StringValue=\"D:\\MailBackup\"");
        StringAssert.Contains(xml, "<Domains>");
        StringAssert.Contains(xml, "<Domain");
        StringAssert.Contains(xml, "Name=\"example.test\"");
        Assert.IsFalse(xml.Contains("DataFiles", StringComparison.Ordinal));
        Assert.IsTrue(
            xml.IndexOf("<Domains>", StringComparison.Ordinal)
            < xml.IndexOf("<Properties>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlWritesEveryModeledSettingsPropertyInLegacyNameOrderWithoutCredentials()
    {
        var settings = new SettingsAdministrationSnapshot(
            HostName: "mail.example.test",
            WelcomeSmtp: "smtp & welcome",
            WelcomePop3: "pop3",
            WelcomeImap: "imap",
            MaxSmtpConnections: 11,
            ServiceSmtp: true,
            ImapSaslInitialResponseEnabled: true,
            ImapHierarchyDelimiter: "/",
            SmtpRelayer: "relay.example.test",
            SmtpRelayerRequiresAuthentication: true,
            SmtpRelayerUsername: "relay-user",
            BackupDestination: @"D:\MailBackup",
            BackupOptions: 1,
            AntiVirusClamAvHost: "127.0.0.1",
            AntiSpamSpamAssassinHost: "127.0.0.1",
            CacheEnabled: true,
            DistributionListCacheTtl: 42);

        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            1,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(settings, Domains: null));
        var properties = XDocument.Parse(xml)
            .Root!
            .Element("Properties")!
            .Elements()
            .ToArray();

        Assert.AreEqual(108, properties.Length);
        CollectionAssert.AreEqual(
            properties.Select(static property => property.Name.LocalName).ToArray(),
            properties.Select(static property => property.Name.LocalName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray());
        Assert.AreEqual("1", properties.Single(property => property.Name == "protocolsmtp")
            .Attribute("LongValue")?.Value);
        Assert.AreEqual(
            "smtp & welcome",
            properties.Single(property => property.Name == "welcomesmtp")
                .Attribute("StringValue")?.Value);
        Assert.AreEqual(
            "42",
            properties.Single(property => property.Name == "distributionlistcachettl")
                .Attribute("LongValue")?.Value);
        Assert.IsFalse(xml.Contains("smtprelayerpassword", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlUsesRawSettingsRowsAndFiltersCredentialBeforeWriting()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            1,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: null,
                SettingsProperties: new[]
                {
                    new BackupSettingsPropertySnapshot("sendstatistics", -7, "a&<\\\"'"),
                    new BackupSettingsPropertySnapshot("relaymode", 2, string.Empty),
                    new BackupSettingsPropertySnapshot("MessageIndexing", 1, string.Empty),
                    new BackupSettingsPropertySnapshot("smtprelayerpassword", 0, "secret")
                }));
        var properties = XDocument.Parse(xml)
            .Root!
            .Element("Properties")!
            .Elements()
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "MessageIndexing", "relaymode", "sendstatistics" },
            properties.Select(static property => property.Name.LocalName).ToArray());
        Assert.AreEqual(
            "-7",
            properties.Single(property => property.Name == "sendstatistics")
                .Attribute("LongValue")?.Value);
        Assert.AreEqual(
            "a&<\\\"'",
            properties.Single(property => property.Name == "sendstatistics")
                .Attribute("StringValue")?.Value);
        Assert.IsFalse(xml.Contains("smtprelayerpassword", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PackagesSettingsAndDomainsFromReadOnlyPayloadProvider()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);

        try
        {
            var settings = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: "smtp",
                WelcomePop3: "pop3",
                WelcomeImap: "imap",
                BackupDestination: destination,
                BackupOptions: 11);
            var runtime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-B0",
                static () => new DateTime(2026, 7, 30, 4, 5, 7),
                (evidence, _) => ValueTask.FromResult(
                    new BackupArchiveXmlPayload(
                        evidence.Settings,
                        Array.Empty<DomainAdministrationSnapshot>())));

            await runtime.CreateAsync(
                new BackupStartPlanEvidence(
                    Destination: destination,
                    BackupOptions: 11,
                    BackupMessagesDbOnly: false,
                    AllMessageFilesInDataDirectory: true,
                    DestinationExists: true,
                    Settings: settings),
                CancellationToken.None);

            var archivePath = Path.Combine(destination, "HMBackup 2026-07-30 040507.7z");
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "DataBackup")));
            Assert.AreEqual(11, new SevenZipBackupArchiveMetadataReader(sevenZipPath)
                .ReadContainsOptions(archivePath));
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [TestMethod]
    public void MetadataXmlWritesScopedDomainAliasesInSuppliedOrderAndOmitsEmptyContainers()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true),
            new DomainAdministrationSnapshot(30, "gamma.example", true)
        };
        var aliases = new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>
        {
            [10] = new[]
            {
                new DomainAliasAdministrationSnapshot(20, 10, "first<&\"'alias.example"),
                new DomainAliasAdministrationSnapshot(10, 10, "second.example")
            },
            [20] = new[]
            {
                new DomainAliasAdministrationSnapshot(30, 20, "alias.beta.example")
            },
            [30] = Array.Empty<DomainAliasAdministrationSnapshot>()
        };

        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: domains,
                DomainAliases: aliases));

        var domainElements = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Elements("Domain")
            .ToArray();
        var firstDomainAliases = domainElements[0]
            .Element("DomainAliases")!
            .Elements("DomainAlias")
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "first<&\"'alias.example", "second.example" },
            firstDomainAliases.Select(static alias => alias.Attribute("Name")!.Value).ToArray());
        Assert.AreEqual(1, firstDomainAliases[0].Attributes().Count());
        Assert.IsNull(firstDomainAliases[0].Attribute("ID"));
        Assert.IsNull(firstDomainAliases[0].Attribute("DomainID"));
        CollectionAssert.AreEqual(
            new[] { "alias.beta.example" },
            domainElements[1]
                .Element("DomainAliases")!
                .Elements("DomainAlias")
                .Select(static alias => alias.Attribute("Name")!.Value)
                .ToArray());
        Assert.IsNull(domainElements[2].Element("DomainAliases"));
        Assert.IsTrue(xml.Contains("first&lt;&amp;&quot;'alias.example", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlWritesLegacyOrderedAccountScalarsAndEscapesValues()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                DomainAliases: new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>
                {
                    [10] = new[] { new DomainAliasAdministrationSnapshot(1, 10, "alias.example.test") }
                },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new AccountAdministrationSnapshot(
                            Id: 1,
                            DomainId: 10,
                            Address: "account<&\"'@example.test",
                            Active: true,
                            AdminLevel: 2,
                            IsActiveDirectoryAccount: true,
                            ActiveDirectoryDomain: "ad<&\"'",
                            ActiveDirectoryUsername: "user<&\"'",
                            MaxSize: 512,
                            LastLogonTime: new DateTime(2026, 7, 30, 14, 15, 16),
                            PersonFirstName: "First<&\"'",
                            PersonLastName: "Last<&\"'",
                            VacationMessageIsOn: true,
                            VacationMessage: "Vacation<&\"'",
                            VacationSubject: "Subject<&\"'",
                            VacationMessageExpires: true,
                            VacationMessageExpiresDate: "2026-08-01",
                            VacationMessageAbortSpamFlagged: true,
                            ForwardEnabled: true,
                            ForwardAddress: "forward<&\"'@example.test",
                            ForwardKeepOriginal: true,
                            ForwardAbortSpamFlagged: true,
                            SignatureEnabled: true,
                            SignaturePlainText: "Plain<&\"'",
                            SignatureHtml: "<p>Html & \" '</p>")
                    }
                },
                BackupAccounts: new Dictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new AccountBackupAdministrationSnapshot(
                            new AccountAdministrationSnapshot(
                                Id: 1,
                                DomainId: 10,
                                Address: "account<&\"'@example.test",
                                Active: true,
                                AdminLevel: 2),
                            Password: "Password<&\"'",
                            PasswordEncryption: 2)
                    }
                },
                Aliases: new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new AliasAdministrationSnapshot(
                            Id: 30,
                            DomainId: 10,
                            Name: "alias<&\"'@example.test",
                            Value: "target<&\"'@example.test",
                            Active: true),
                        new AliasAdministrationSnapshot(
                            Id: 31,
                            DomainId: 10,
                            Name: "second@example.test",
                            Value: "inactive@example.test",
                            Active: false)
                    }
                }));

        var account = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!;
        var domain = account.Parent!.Parent!;

        CollectionAssert.AreEqual(
            new[] { "DomainAliases", "Accounts", "Aliases" },
            domain.Elements().Select(static element => element.Name.LocalName).ToArray());

        var aliases = domain.Element("Aliases")!.Elements("Alias").ToArray();
        CollectionAssert.AreEqual(
            new[] { "Name", "Value", "Active" },
            aliases[0].Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        CollectionAssert.AreEqual(
            new[] { "alias<&\"'@example.test", "second@example.test" },
            aliases.Select(static alias => alias.Attribute("Name")!.Value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "target<&\"'@example.test", "inactive@example.test" },
            aliases.Select(static alias => alias.Attribute("Value")!.Value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "1", "0" },
            aliases.Select(static alias => alias.Attribute("Active")!.Value).ToArray());
        Assert.IsNull(aliases[0].Attribute("ID"));
        Assert.IsNull(aliases[0].Attribute("DomainID"));
        Assert.IsTrue(xml.Contains("Name=\"alias&lt;&amp;&quot;'@example.test\"", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("Value=\"target&lt;&amp;&quot;'@example.test\"", StringComparison.Ordinal));

        CollectionAssert.AreEqual(
            new[]
            {
                "Name", "PersonFirstName", "PersonLastName", "Active", "Password", "PasswordEncryption", "MaxAccountSize",
                "ADUsername", "ADDomain", "ADActive", "VacationMessageOn", "VacationMessage",
                "VacationSubject", "VacationExpires", "VacationExpireDate", "VacationAbortSpamFlagged",
                "AdminLevel", "ForwardEnabled", "ForwardAddress", "ForwardKeepOriginal",
                "ForwardAbortSpamFlagged", "EnableSignature", "SignaturePlainText", "SignatureHTML",
                "LastLogonTime"
            },
            account.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("2026-07-30 14:15:16", account.Attribute("LastLogonTime")?.Value);
        Assert.AreEqual("Password<&\"'", account.Attribute("Password")?.Value);
        Assert.AreEqual("2", account.Attribute("PasswordEncryption")?.Value);
        Assert.AreEqual("account<&\"'@example.test", account.Attribute("Name")?.Value);
        Assert.IsTrue(xml.Contains("Name=\"account&lt;&amp;&quot;'@example.test\"", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("Password=\"Password&lt;&amp;&quot;'\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlOmitsEmptyAccountContainers()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true)
        };
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: domains,
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = Array.Empty<AccountAdministrationSnapshot>(),
                    [20] = new[] { new AccountAdministrationSnapshot(1, 20, "account@example.test", true, 0) }
                }));

        var domainElements = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Elements("Domain")
            .ToArray();
        Assert.IsNull(domainElements[0].Element("Accounts"));
        var account = domainElements[1].Element("Accounts")!.Element("Account")!;
        Assert.AreEqual("account@example.test", account.Attribute("Name")?.Value);
        Assert.IsNull(account.Attribute("Password"));
        Assert.IsNull(account.Attribute("PasswordEncryption"));
    }

    [TestMethod]
    public void MetadataXmlWritesNonSecretFetchAccountScalarsInLegacyOrderAndEscapesValues()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                FetchAccounts: new Dictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>
                {
                    [20] = new[]
                    {
                        new FetchAccountAdministrationSnapshot(
                            Id: 30,
                            AccountId: 20,
                            Name: "fetch<&\"'",
                            ServerAddress: "pop3<&\"'",
                            Port: 995,
                            ServerType: 0,
                            Username: "user<&\"'",
                            MinutesBetweenFetch: 15,
                            DaysToKeepMessages: 7,
                            Enabled: true,
                            ProcessMimeRecipients: false,
                            ProcessMimeDate: true,
                            ConnectionSecurity: 2,
                            UseAntiSpam: true,
                            UseAntiVirus: false,
                            EnableRouteRecipients: true,
                            MimeRecipientHeaders: "To,CC<&\"'",
                            NextDownloadTime: "2026-07-30 01:02:03",
                            IsLocked: true)
                    }
                }));

        var fetchAccount = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!
            .Element("FetchAccounts")!
            .Element("FetchAccount")!;

        CollectionAssert.AreEqual(
            new[]
            {
                "Name", "ServerAddress", "ServerType", "Port", "Username", "Minutes", "DaysToKeep",
                "Active", "MIMERecipientHeaders", "ProcessMIMERecipients", "ProcessMIMEDate", "UseAntiSpam",
                "UseAntiVirus", "EnableRouteRecipients", "ConnectionSecurity"
            },
            fetchAccount.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("fetch<&\"'", fetchAccount.Attribute("Name")?.Value);
        Assert.AreEqual("995", fetchAccount.Attribute("Port")?.Value);
        Assert.AreEqual("1", fetchAccount.Attribute("Active")?.Value);
        Assert.AreEqual("2", fetchAccount.Attribute("ConnectionSecurity")?.Value);
        Assert.IsNull(fetchAccount.Attribute("Password"));
        Assert.IsFalse(xml.Contains("FetchAccountUID", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("Name=\"fetch&lt;&amp;&quot;'\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlOmitsEmptyFetchAccountContainers()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                FetchAccounts: new Dictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>
                {
                    [20] = Array.Empty<FetchAccountAdministrationSnapshot>()
                }));

        var account = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!;

        Assert.IsNull(account.Element("FetchAccounts"));
    }

    [TestMethod]
    public void MetadataXmlWritesLegacyRulesCriteriaAndActionsInOrderAndEscapesValues()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                FetchAccounts: new Dictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>
                {
                    [20] = new[] { CreateFetchAccountSnapshot(30, 20, "fetch") }
                },
                Rules: new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>
                {
                    [20] = new[]
                    {
                        new RuleAdministrationSnapshot(101, 20, "rule<&\"'", true, false, 2),
                        new RuleAdministrationSnapshot(102, 20, "second", false, true, 3)
                    }
                },
                RuleCriterias: new Dictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>
                {
                    [101] = new[]
                    {
                        new RuleCriteriaAdministrationSnapshot(201, 101, "match<&\"'", true, 4, 5, "X-Test<&\"'")
                    }
                },
                RuleActions: new Dictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>
                {
                    [101] = new[]
                    {
                        new RuleActionAdministrationSnapshot(
                            301,
                            101,
                            7,
                            "subject<&\"'",
                            "body<&\"'",
                            "from-name<&\"'",
                            "from-address<&\"'",
                            "file<&\"'",
                            "to<&\"'",
                            "folder<&\"'",
                            "script<&\"'",
                            "header<&\"'",
                            "value<&\"'",
                            12,
                            true,
                            1)
                    }
                }));

        var account = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!;
        CollectionAssert.AreEqual(
            new[] { "FetchAccounts", "Rules" },
            account.Elements().Select(static element => element.Name.LocalName).ToArray());

        var rules = account.Element("Rules")!.Elements("Rule").ToArray();
        CollectionAssert.AreEqual(
            new[] { "rule<&\"'", "second" },
            rules.Select(static rule => rule.Attribute("Name")!.Value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Name", "Active", "UseAND", "SortOrder" },
            rules[0].Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        CollectionAssert.AreEqual(
            new[] { "RuleCriterias", "RuleActions" },
            rules[0].Elements().Select(static element => element.Name.LocalName).ToArray());
        CollectionAssert.AreEqual(
            new[] { "MatchString", "FieldType", "MatchType", "HeaderField", "UsePredefinedField" },
            rules[0].Element("RuleCriterias")!.Element("Criteria")!
                .Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Type", "Subject", "Body", "FromAddress", "FromName", "IMAPFolder", "FileName", "To",
                "ScriptFunction", "SortOrder", "Header", "Value", "RouteID", "AbortSpamFlagged"
            },
            rules[0].Element("RuleActions")!.Element("Action")!
                .Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.IsTrue(xml.Contains("Name=\"rule&lt;&amp;&quot;&apos;\"", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("MatchString=\"match&lt;&amp;&quot;&apos;\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlWritesRootFolderScalarsAfterRulesInLegacyOrder()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            6,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                Rules: new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>
                {
                    [20] = new[] { new RuleAdministrationSnapshot(101, 20, "rule", true, true, 1) }
                },
                Folders: new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>
                {
                    [20] = new[]
                    {
                        new ImapFolderAdministrationSnapshot(
                            301,
                            20,
                            -1,
                            "root<&\"' >",
                            true,
                            42,
                            "2026-07-30 01:02:03"),
                        new ImapFolderAdministrationSnapshot(
                            302,
                            20,
                            -1,
                            "second",
                            false,
                            7,
                            "2026-07-30 02:03:04"),
                        new ImapFolderAdministrationSnapshot(
                            303,
                            20,
                            301,
                            "child",
                            true,
                            11,
                            "2026-07-30 03:04:05"),
                        new ImapFolderAdministrationSnapshot(
                            304,
                            20,
                            303,
                            "grandchild",
                            false,
                            12,
                            "2026-07-30 04:05:06")
                    }
                },
                FolderMessages: new Dictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>
                {
                    [301] = new[]
                    {
                        new MessageAdministrationSnapshot(
                            Id: 401,
                            AccountId: 20,
                            FolderId: 301,
                            FileName: @"C:\hMailServer\Data\account\message<&""'.eml",
                            State: 2,
                            FromAddress: "from<&\"'@example.test",
                            SizeBytes: 1234,
                            CurrentNumberOfTries: 3,
                            Flags: 5,
                            InternalDate: new DateTime(2026, 7, 30, 5, 6, 7),
                            Uid: 12)
                    }
                }));

        var account = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!;
        CollectionAssert.AreEqual(
            new[] { "Rules", "Folders" },
            account.Elements().Select(static element => element.Name.LocalName).ToArray());

        var folders = account.Element("Folders")!.Elements("Folder").ToArray();
        CollectionAssert.AreEqual(
            new[] { "Name", "Subscribed", "CreateTime", "CurrentUID" },
            folders[0].Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("root<&\"' >", folders[0].Attribute("Name")?.Value);
        Assert.AreEqual("1", folders[0].Attribute("Subscribed")?.Value);
        Assert.AreEqual("2026-07-30 01:02:03", folders[0].Attribute("CreateTime")?.Value);
        Assert.AreEqual("42", folders[0].Attribute("CurrentUID")?.Value);
        Assert.AreEqual("0", folders[1].Attribute("Subscribed")?.Value);
        Assert.AreEqual("child", folders[0].Element("Folders")!.Element("Folder")!.Attribute("Name")?.Value);
        Assert.AreEqual(
            "grandchild",
            folders[0].Element("Folders")!.Element("Folder")!.Element("Folders")!
                .Element("Folder")!.Attribute("Name")?.Value);
        Assert.IsNull(folders[1].Element("Folders"));
        CollectionAssert.AreEqual(
            new[] { "Messages", "Folders" },
            folders[0].Elements().Select(static element => element.Name.LocalName).ToArray());
        var message = folders[0].Element("Messages")!.Element("Message")!;
        CollectionAssert.AreEqual(
            new[] { "CreateTime", "Filename", "FromAddress", "State", "Size", "NoOfRetries", "Flags", "ID", "UID" },
            message.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("2026-07-30 05:06:07", message.Attribute("CreateTime")?.Value);
        Assert.AreEqual("message<&\"'.eml", message.Attribute("Filename")?.Value);
        Assert.AreEqual("2", message.Attribute("State")?.Value);
        Assert.AreEqual("1234", message.Attribute("Size")?.Value);
        Assert.AreEqual("401", message.Attribute("ID")?.Value);
        Assert.AreEqual("12", message.Attribute("UID")?.Value);
        Assert.IsTrue(xml.Contains("Filename=\"message&lt;&amp;&quot;&apos;.eml\"", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("Name=\"root&lt;&amp;&quot;&apos; &gt;\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MetadataXmlOmitsFolderContainerWhenEmptyOrMessagesAreNotSelected()
    {
        var emptyFoldersXml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            6,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                Folders: new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>
                {
                    [20] = Array.Empty<ImapFolderAdministrationSnapshot>()
                }));

        var folderXmlWithoutMessages = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                Folders: new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>
                {
                    [20] = new[]
                    {
                        new ImapFolderAdministrationSnapshot(301, 20, -1, "root", true, 42, "2026-07-30 01:02:03")
                    }
                }));

        Assert.IsNull(XDocument.Parse(emptyFoldersXml).Root!.Element("Domains")!.Element("Domain")!
            .Element("Accounts")!.Element("Account")!.Element("Folders"));
        Assert.IsNull(XDocument.Parse(folderXmlWithoutMessages).Root!.Element("Domains")!.Element("Domain")!
            .Element("Accounts")!.Element("Account")!.Element("Folders"));
    }

    [TestMethod]
    public void MetadataXmlOmitsEmptyMessageContainers()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            6,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(20, 10, "account@example.test", true, 0) }
                },
                Folders: new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>
                {
                    [20] = new[] { new ImapFolderAdministrationSnapshot(301, 20, -1, "root", true, 42, "2026-07-30 01:02:03") }
                },
                FolderMessages: new Dictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>
                {
                    [301] = Array.Empty<MessageAdministrationSnapshot>()
                }));

        var folder = XDocument.Parse(xml).Root!.Element("Domains")!.Element("Domain")!
            .Element("Accounts")!.Element("Account")!.Element("Folders")!.Element("Folder")!;
        Assert.IsNull(folder.Element("Messages"));
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsAllFoldersOnceForSelectedAccountsOnlyWhenMessagesAreSelected()
    {
        var folderStore = new RecordingImapFolderAdministrationStore(
            new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>
            {
                [1] = new[]
                {
                    new ImapFolderAdministrationSnapshot(101, 1, -1, "one", true, 1, "2026-07-30 01:02:03"),
                    new ImapFolderAdministrationSnapshot(103, 1, 101, "one-child", false, 3, "2026-07-30 01:02:04")
                },
                [2] = new[] { new ImapFolderAdministrationSnapshot(102, 2, -1, "two", false, 2, "2026-07-30 01:02:04") },
                [99] = new[] { new ImapFolderAdministrationSnapshot(199, 99, -1, "outside", true, 9, "2026-07-30 01:02:05") }
            });
        var messageStore = new RecordingMessageAdministrationStore(
            new Dictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>
            {
                [101] = new[] { new MessageAdministrationSnapshot(401, 1, 101, "one.eml", 2, "one@example.test", 10, 0, 1, new DateTime(2026, 7, 30, 1, 2, 3), 1) },
                [103] = new[] { new MessageAdministrationSnapshot(403, 1, 103, "child.eml", 2, "child@example.test", 20, 0, 2, new DateTime(2026, 7, 30, 1, 2, 4), 2) },
                [102] = new[] { new MessageAdministrationSnapshot(402, 2, 102, "two.eml", 2, "two@example.test", 30, 0, 3, new DateTime(2026, 7, 30, 1, 2, 5), 3) }
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(
                new[]
                {
                    new DomainAdministrationSnapshot(10, "example.test", true),
                    new DomainAdministrationSnapshot(10, "example.test", true)
                }),
            new RecordingDomainAliasAdministrationStore(new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new AccountAdministrationSnapshot(1, 10, "one@example.test", true, 0),
                        new AccountAdministrationSnapshot(2, 10, "two@example.test", true, 0)
                    }
                }),
            new RecordingAliasAdministrationStore(new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            null,
            null,
            folderStore: folderStore,
            messageStore: messageStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 6,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2 }, folderStore.RequestedAccountIds.ToArray());
        Assert.IsNotNull(payload.Folders);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, payload.Folders!.Keys.ToArray());
        Assert.AreEqual(2, payload.Folders[1].Count);
        Assert.AreEqual(101, payload.Folders[1][1].ParentId);
        CollectionAssert.AreEqual(new[] { 101, 103, 102 }, messageStore.RequestedFolderIds.ToArray());
        Assert.AreEqual(1, payload.FolderMessages![101].Count);

        var noMessagesFolderStore = new RecordingImapFolderAdministrationStore(
            new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>());
        var noMessagesMessageStore = new RecordingMessageAdministrationStore(
            new Dictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>());
        var noMessagesRuntime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(new[] { new DomainAdministrationSnapshot(10, "example.test", true) }),
            new RecordingDomainAliasAdministrationStore(new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(1, 10, "one@example.test", true, 0) }
                }),
            new RecordingAliasAdministrationStore(new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            null,
            null,
            folderStore: noMessagesFolderStore,
            messageStore: noMessagesMessageStore);

        var noMessagesPayload = await noMessagesRuntime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<int>(), noMessagesFolderStore.RequestedAccountIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), noMessagesMessageStore.RequestedFolderIds.ToArray());
        Assert.IsNull(noMessagesPayload.Folders);
        Assert.IsNull(noMessagesPayload.FolderMessages);
    }

    [TestMethod]
    public void MetadataXmlOmitsEmptyRulesCriteriaAndActionContainers()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new AccountAdministrationSnapshot(20, 10, "with-rule@example.test", true, 0),
                        new AccountAdministrationSnapshot(21, 10, "without-rule@example.test", true, 0)
                    }
                },
                Rules: new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>
                {
                    [20] = new[] { new RuleAdministrationSnapshot(101, 20, "rule", true, true, 1) },
                    [21] = Array.Empty<RuleAdministrationSnapshot>()
                },
                RuleCriterias: new Dictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>
                {
                    [101] = Array.Empty<RuleCriteriaAdministrationSnapshot>()
                },
                RuleActions: new Dictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>
                {
                    [101] = Array.Empty<RuleActionAdministrationSnapshot>()
                }));

        var accounts = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Elements("Account")
            .ToArray();
        Assert.IsNotNull(accounts[0].Element("Rules"));
        Assert.IsNull(accounts[0].Element("Rules")!.Element("Rule")!.Element("RuleCriterias"));
        Assert.IsNull(accounts[0].Element("Rules")!.Element("Rule")!.Element("RuleActions"));
        Assert.IsNull(accounts[1].Element("Rules"));
    }

    [TestMethod]
    public void MetadataXmlOmitsEmptyAliasContainers()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "example.test", true) },
                Aliases: new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>
                {
                    [10] = Array.Empty<AliasAdministrationSnapshot>()
                }));

        var domain = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!;

        Assert.IsNull(domain.Element("Aliases"));
    }

    [TestMethod]
    public void MetadataXmlWritesOrderedDistributionListsAndRecipientsAndOmitsEmptyContainers()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[]
                {
                    new DomainAdministrationSnapshot(10, "example.test", true),
                    new DomainAdministrationSnapshot(20, "empty.example.test", true)
                },
                DomainAliases: new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>
                {
                    [10] = new[] { new DomainAliasAdministrationSnapshot(1, 10, "alias.example.test") }
                },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(2, 10, "account@example.test", true, 0) }
                },
                Aliases: new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>
                {
                    [10] = new[] { new AliasAdministrationSnapshot(3, 10, "alias@example.test", "target@example.test", true) }
                },
                DistributionLists: new Dictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new DistributionListAdministrationSnapshot(
                            100,
                            10,
                            "list<&\"'@example.test",
                            true,
                            true,
                            "sender<&\"'@example.test",
                            2),
                        new DistributionListAdministrationSnapshot(
                            101,
                            10,
                            "empty-list@example.test",
                            false,
                            false,
                            "",
                            0)
                    },
                    [20] = Array.Empty<DistributionListAdministrationSnapshot>()
                },
                DistributionListRecipients: new Dictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>
                {
                    [100] = new[]
                    {
                        new DistributionListRecipientAdministrationSnapshot(200, 100, "recipient<&\"'@example.test")
                    },
                    [101] = Array.Empty<DistributionListRecipientAdministrationSnapshot>()
                }));

        var domains = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Elements("Domain")
            .ToArray();
        var domain = domains[0];
        CollectionAssert.AreEqual(
            new[] { "DomainAliases", "Accounts", "Aliases", "DistributionLists" },
            domain.Elements().Select(static element => element.Name.LocalName).ToArray());

        var lists = domain.Element("DistributionLists")!.Elements("DistributionList").ToArray();
        CollectionAssert.AreEqual(
            new[] { "Name", "Active", "RequiresAuth", "RequiresAuthAddress", "ListMode" },
            lists[0].Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("list<&\"'@example.test", lists[0].Attribute("Name")?.Value);
        Assert.AreEqual("1", lists[0].Attribute("Active")?.Value);
        Assert.AreEqual("1", lists[0].Attribute("RequiresAuth")?.Value);
        Assert.AreEqual("sender<&\"'@example.test", lists[0].Attribute("RequiresAuthAddress")?.Value);
        Assert.AreEqual("2", lists[0].Attribute("ListMode")?.Value);
        Assert.IsNull(lists[0].Attribute("ID"));
        Assert.IsNull(lists[0].Attribute("DomainID"));

        var recipientsContainer = lists[0].Element("DistributionList")!;
        var recipient = recipientsContainer.Element("Recipient")!;
        CollectionAssert.AreEqual(
            new[] { "Name" },
            recipient.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("recipient<&\"'@example.test", recipient.Attribute("Name")?.Value);
        Assert.IsNull(lists[1].Element("DistributionList"));
        Assert.IsNull(domains[1].Element("DistributionLists"));
        Assert.IsTrue(xml.Contains("Name=\"list&lt;&amp;&quot;'@example.test\"", StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains("Name=\"recipient&lt;&amp;&quot;'@example.test\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsAliasesOnceForEachSelectedDomainId()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true)
        };
        var aliasStore = new RecordingDomainAliasAdministrationStore(
            new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>
            {
                [10] = new[] { new DomainAliasAdministrationSnapshot(1, 10, "alias.alpha.example") },
                [20] = Array.Empty<DomainAliasAdministrationSnapshot>(),
                [99] = new[] { new DomainAliasAdministrationSnapshot(2, 99, "not-selected.example") }
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            aliasStore,
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>()),
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()));

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 10, 20 }, aliasStore.RequestedDomainIds.ToArray());
        Assert.IsNotNull(payload.DomainAliases);
        Assert.AreEqual(2, payload.DomainAliases.Count);
        CollectionAssert.AreEqual(
            new[] { "alias.alpha.example" },
            payload.DomainAliases[10].Select(static alias => alias.AliasName).ToArray());
        Assert.AreEqual(0, payload.DomainAliases[20].Count);
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsNormalAliasesOncePerSelectedDomainIdAndScopesPayload()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true),
            new DomainAdministrationSnapshot(10, "alpha-duplicate.example", true)
        };
        var aliasStore = new RecordingAliasAdministrationStore(
            new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>
            {
                [10] = new[]
                {
                    new AliasAdministrationSnapshot(30, 10, "sales@alpha.example", "user@alpha.example", true)
                },
                [20] = Array.Empty<AliasAdministrationSnapshot>(),
                [99] = new[]
                {
                    new AliasAdministrationSnapshot(31, 99, "outside@example.test", "user@example.test", true)
                }
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>()),
            aliasStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 10, 20 }, aliasStore.RequestedDomainIds.ToArray());
        Assert.IsNotNull(payload.Aliases);
        CollectionAssert.AreEqual(new[] { 10, 20 }, payload.Aliases.Keys.OrderBy(static id => id).ToArray());
        CollectionAssert.AreEqual(
            new[] { "sales@alpha.example" },
            payload.Aliases[10].Select(static alias => alias.Name).ToArray());
        Assert.AreEqual(0, payload.Aliases[20].Count);
        Assert.IsFalse(payload.Aliases.ContainsKey(99));
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsDistributionListsAndRecipientsOnceAndScopesPayload()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true),
            new DomainAdministrationSnapshot(10, "alpha-duplicate.example", true)
        };
        var listStore = new RecordingDistributionListAdministrationStore(
            new Dictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>
            {
                [10] = new[]
                {
                    new DistributionListAdministrationSnapshot(100, 10, "list1@alpha.example", true, false, "", 0),
                    new DistributionListAdministrationSnapshot(100, 10, "list1@alpha.example", true, false, "", 0),
                    new DistributionListAdministrationSnapshot(101, 10, "list2@alpha.example", false, true, "sender@alpha.example", 1)
                },
                [20] = Array.Empty<DistributionListAdministrationSnapshot>(),
                [99] = new[] { new DistributionListAdministrationSnapshot(999, 99, "outside@example.test", true, false, "", 0) }
            });
        var recipientStore = new RecordingDistributionListRecipientAdministrationStore(
            new Dictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>
            {
                [100] = new[] { new DistributionListRecipientAdministrationSnapshot(200, 100, "recipient@alpha.example") },
                [101] = Array.Empty<DistributionListRecipientAdministrationSnapshot>(),
                [999] = new[] { new DistributionListRecipientAdministrationSnapshot(299, 999, "outside@example.test") }
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>()),
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            listStore,
            recipientStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 10, 20 }, listStore.RequestedDomainIds.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 101 }, recipientStore.RequestedListIds.ToArray());
        Assert.IsNotNull(payload.DistributionLists);
        CollectionAssert.AreEqual(new[] { 10, 20 }, payload.DistributionLists.Keys.OrderBy(static id => id).ToArray());
        CollectionAssert.AreEqual(
            new[] { "list1@alpha.example", "list1@alpha.example", "list2@alpha.example" },
            payload.DistributionLists[10].Select(static list => list.Address).ToArray());
        Assert.AreEqual(0, payload.DistributionLists[20].Count);
        Assert.IsNotNull(payload.DistributionListRecipients);
        CollectionAssert.AreEqual(new[] { 100, 101 }, payload.DistributionListRecipients.Keys.OrderBy(static id => id).ToArray());
        Assert.IsFalse(payload.DistributionListRecipients.ContainsKey(999));
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsAccountsOnceForEachSelectedDomainId()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true),
            new DomainAdministrationSnapshot(10, "alpha-duplicate.example", true)
        };
        var accountStore = new RecordingAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
            {
                [10] = new[] { new AccountAdministrationSnapshot(1, 10, "first@alpha.example", true, 0) },
                [20] = Array.Empty<AccountAdministrationSnapshot>()
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            accountStore,
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()));

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 10, 20 }, accountStore.RequestedDomainIds.ToArray());
        Assert.IsNotNull(payload.Accounts);
        CollectionAssert.AreEqual(
            new[] { "first@alpha.example" },
            payload.Accounts[10].Select(static account => account.Address).ToArray());
        Assert.AreEqual(0, payload.Accounts[20].Count);
    }

    [TestMethod]
    public async Task PayloadRuntimeUsesDedicatedBackupAccountStoreForAccountsAndCredentials()
    {
        var domains = new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) };
        var ordinaryAccountStore = new RecordingAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>());
        var backupAccountStore = new RecordingBackupAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>
            {
                [10] = new[]
                {
                    new AccountBackupAdministrationSnapshot(
                        new AccountAdministrationSnapshot(1, 10, "account@alpha.example", true, 0),
                        "secret<&\"'",
                        1)
                }
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            ordinaryAccountStore,
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            distributionListStore: null,
            distributionListRecipientStore: null,
            backupAccountStore: backupAccountStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<int>(), ordinaryAccountStore.RequestedDomainIds.ToArray());
        CollectionAssert.AreEqual(new[] { 10 }, backupAccountStore.RequestedDomainIds.ToArray());
        Assert.IsNotNull(payload.BackupAccounts);
        Assert.AreEqual("secret<&\"'", payload.BackupAccounts[10][0].Password);
        Assert.AreEqual(1, payload.BackupAccounts[10][0].PasswordEncryption);
        Assert.AreEqual("account@alpha.example", payload.Accounts![10][0].Address);

        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(2, "10.0.0-B0", payload);
        var account = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!;
        CollectionAssert.AreEqual(
            new[]
            {
                "Name", "PersonFirstName", "PersonLastName", "Active", "Password", "PasswordEncryption",
                "MaxAccountSize", "ADUsername", "ADDomain", "ADActive", "VacationMessageOn", "VacationMessage",
                "VacationSubject", "VacationExpires", "VacationExpireDate", "VacationAbortSpamFlagged", "AdminLevel",
                "ForwardEnabled", "ForwardAddress", "ForwardKeepOriginal", "ForwardAbortSpamFlagged", "EnableSignature",
                "SignaturePlainText", "SignatureHTML", "LastLogonTime"
            },
            account.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("secret<&\"'", account.Attribute("Password")?.Value);
        Assert.AreEqual("1", account.Attribute("PasswordEncryption")?.Value);
        Assert.IsTrue(xml.Contains("Password=\"secret&lt;&amp;&quot;'\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsFetchAccountsOnceForEachSelectedAccountAndScopesPayload()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true),
            new DomainAdministrationSnapshot(10, "alpha-duplicate.example", true)
        };
        var accountStore = new RecordingAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
            {
                [10] = new[] { new AccountAdministrationSnapshot(1, 10, "first@alpha.example", true, 0) },
                [20] = new[] { new AccountAdministrationSnapshot(2, 20, "second@beta.example", true, 0) },
                [99] = new[] { new AccountAdministrationSnapshot(3, 99, "outside@example.test", true, 0) }
            });
        var fetchAccountStore = new RecordingFetchAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>
            {
                [1] = new[] { CreateFetchAccountSnapshot(11, 1, "first-fetch") },
                [2] = Array.Empty<FetchAccountAdministrationSnapshot>(),
                [3] = new[] { CreateFetchAccountSnapshot(33, 3, "outside-fetch") }
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            accountStore,
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            distributionListStore: null,
            distributionListRecipientStore: null,
            fetchAccountStore: fetchAccountStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2 }, fetchAccountStore.RequestedAccountIds.ToArray());
        Assert.IsNotNull(payload.FetchAccounts);
        CollectionAssert.AreEqual(new[] { 1, 2 }, payload.FetchAccounts.Keys.OrderBy(static id => id).ToArray());
        Assert.AreEqual("first-fetch", payload.FetchAccounts[1][0].Name);
        Assert.AreEqual(0, payload.FetchAccounts[2].Count);
        Assert.IsFalse(payload.FetchAccounts.ContainsKey(3));
    }

    [TestMethod]
    public async Task PayloadRuntimeLoadsBackupRulesAndNestedChildrenOnceAndScopesToSelectedAccounts()
    {
        var domains = new[]
        {
            new DomainAdministrationSnapshot(10, "alpha.example", true),
            new DomainAdministrationSnapshot(20, "beta.example", true),
            new DomainAdministrationSnapshot(10, "alpha-duplicate.example", true)
        };
        var accountStore = new RecordingAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
            {
                [10] = new[]
                {
                    new AccountAdministrationSnapshot(1, 10, "first@alpha.example", true, 0),
                    new AccountAdministrationSnapshot(2, 10, "second@alpha.example", true, 0)
                },
                [20] = new[] { new AccountAdministrationSnapshot(3, 20, "third@beta.example", true, 0) },
                [99] = new[] { new AccountAdministrationSnapshot(4, 99, "outside@example.test", true, 0) }
            });
        var ruleStore = new RecordingBackupRuleAdministrationStore(
            new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>
            {
                [1] = new[]
                {
                    new RuleAdministrationSnapshot(11, 1, "first-rule", true, true, 1),
                    new RuleAdministrationSnapshot(12, 1, "second-rule", false, false, 2)
                },
                [2] = Array.Empty<RuleAdministrationSnapshot>(),
                [3] = new[] { new RuleAdministrationSnapshot(13, 3, "third-rule", true, false, 1) },
                [4] = new[] { new RuleAdministrationSnapshot(14, 4, "outside-rule", true, true, 1) }
            });
        var criteriaStore = new RecordingRuleCriteriaAdministrationStore(
            new Dictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>
            {
                [11] = new[] { new RuleCriteriaAdministrationSnapshot(21, 11, "match", true, 1, 2, "") },
                [12] = Array.Empty<RuleCriteriaAdministrationSnapshot>(),
                [13] = Array.Empty<RuleCriteriaAdministrationSnapshot>()
            });
        var actionStore = new RecordingRuleActionAdministrationStore(
            new Dictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>
            {
                [11] = new[] { CreateRuleActionSnapshot(31, 11, "first-action") },
                [12] = Array.Empty<RuleActionAdministrationSnapshot>(),
                [13] = Array.Empty<RuleActionAdministrationSnapshot>()
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            accountStore,
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            distributionListStore: null,
            distributionListRecipientStore: null,
            backupRuleStore: ruleStore,
            ruleCriteriaStore: criteriaStore,
            ruleActionStore: actionStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ruleStore.RequestedAccountIds.ToArray());
        CollectionAssert.AreEqual(new[] { 11, 12, 13 }, criteriaStore.RequestedRuleIds.ToArray());
        CollectionAssert.AreEqual(new[] { 11, 12, 13 }, actionStore.RequestedRuleIds.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, payload.Rules!.Keys.OrderBy(static id => id).ToArray());
        Assert.AreEqual("first-rule", payload.Rules[1][0].Name);
        Assert.IsFalse(payload.Rules.ContainsKey(4));
        Assert.AreEqual("first-action", payload.RuleActions![11][0].Subject);
        Assert.AreEqual("match", payload.RuleCriterias![11][0].MatchValue);
    }

    [TestMethod]
    public async Task PayloadRuntimeDoesNotLoadRulesWhenDomainsAreNotSelected()
    {
        var ruleStore = new RecordingBackupRuleAdministrationStore(
            new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>());
        var criteriaStore = new RecordingRuleCriteriaAdministrationStore(
            new Dictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>());
        var actionStore = new RecordingRuleActionAdministrationStore(
            new Dictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>());
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(Array.Empty<DomainAdministrationSnapshot>()),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>()),
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            distributionListStore: null,
            distributionListRecipientStore: null,
            backupRuleStore: ruleStore,
            ruleCriteriaStore: criteriaStore,
            ruleActionStore: actionStore);

        await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 1,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<int>(), ruleStore.RequestedAccountIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), criteriaStore.RequestedRuleIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), actionStore.RequestedRuleIds.ToArray());
    }

    [TestMethod]
    public async Task PayloadRuntimeUsesDedicatedBackupFetchStoreAndWritesLegacyPasswordAndUidOrder()
    {
        var domains = new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) };
        var ordinaryFetchStore = new RecordingFetchAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>
            {
                [1] = new[] { CreateFetchAccountSnapshot(99, 1, "ordinary-fetch") }
            });
        var backupFetchStore = new RecordingBackupFetchAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>
            {
                [1] = new[]
                {
                    new FetchAccountBackupAdministrationSnapshot(
                        CreateFetchAccountSnapshot(11, 1, "backup-fetch"),
                        "a62b3c438efae3db",
                        new[]
                        {
                            new FetchAccountUidBackupAdministrationSnapshot(
                                "uid<&\"'", "2026-07-30 01:02:03")
                        })
                },
                [2] = Array.Empty<FetchAccountBackupAdministrationSnapshot>()
            });
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(domains),
            new RecordingDomainAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>()),
            new RecordingAccountAdministrationStore(
                new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[]
                    {
                        new AccountAdministrationSnapshot(1, 10, "first@alpha.example", true, 0),
                        new AccountAdministrationSnapshot(2, 10, "second@alpha.example", true, 0)
                    }
                }),
            new RecordingAliasAdministrationStore(
                new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>()),
            distributionListStore: null,
            distributionListRecipientStore: null,
            fetchAccountStore: ordinaryFetchStore,
            backupFetchAccountStore: backupFetchStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 2,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<int>(), ordinaryFetchStore.RequestedAccountIds.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2 }, backupFetchStore.RequestedAccountIds.ToArray());
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(2, "10.0.0-B0", payload);
        var fetchAccount = XDocument.Parse(xml)
            .Root!
            .Element("Domains")!
            .Element("Domain")!
            .Element("Accounts")!
            .Element("Account")!
            .Element("FetchAccounts")!
            .Element("FetchAccount")!;

        CollectionAssert.AreEqual(
            new[]
            {
                "Name", "ServerAddress", "ServerType", "Port", "Username", "Password", "Minutes",
                "DaysToKeep", "Active", "MIMERecipientHeaders", "ProcessMIMERecipients", "ProcessMIMEDate",
                "UseAntiSpam", "UseAntiVirus", "EnableRouteRecipients", "ConnectionSecurity"
            },
            fetchAccount.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("a62b3c438efae3db", fetchAccount.Attribute("Password")?.Value);
        var uids = fetchAccount.Element("FetchAccountUIDs")!;
        CollectionAssert.AreEqual(
            new[] { "UID", "Date" },
            uids.Elements("UID").Single().Attributes().Select(static attribute => attribute.Name.LocalName).ToArray());
        Assert.AreEqual("uid<&\"'", uids.Element("UID")?.Attribute("UID")?.Value);
        Assert.AreEqual("2026-07-30 01:02:03", uids.Element("UID")?.Attribute("Date")?.Value);
        Assert.IsTrue(xml.Contains("UID=\"uid&lt;&amp;&quot;'\"", StringComparison.Ordinal));
        Assert.IsNull(
            XDocument.Parse(xml).Root!.Element("Domains")!.Element("Domain")!
                .Element("Accounts")!.Elements("Account").Skip(1).Single().Element("FetchAccounts"));
    }

    [TestMethod]
    public void MetadataXmlOmitsEmptyFetchAccountAndUidContainers()
    {
        var xml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(1, 10, "account@alpha.example", true, 0) }
                },
                BackupFetchAccounts: new Dictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>
                {
                    [1] = new[]
                    {
                        new FetchAccountBackupAdministrationSnapshot(
                            CreateFetchAccountSnapshot(11, 1, "fetch"),
                            string.Empty,
                            Array.Empty<FetchAccountUidBackupAdministrationSnapshot>())
                    }
                }));

        var account = XDocument.Parse(xml).Root!.Element("Domains")!.Element("Domain")!
            .Element("Accounts")!.Element("Account")!;
        Assert.IsNull(account.Element("FetchAccountUIDs"));
        Assert.IsNotNull(account.Element("FetchAccounts"));
        Assert.AreEqual(string.Empty,
            account.Element("FetchAccounts")!.Element("FetchAccount")!.Attribute("Password")?.Value);

        var emptyXml = SevenZipBackupArchiveRuntime.CreateMetadataXml(
            2,
            "10.0.0-B0",
            new BackupArchiveXmlPayload(
                Settings: null,
                Domains: new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
                Accounts: new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>
                {
                    [10] = new[] { new AccountAdministrationSnapshot(1, 10, "account@alpha.example", true, 0) }
                },
                BackupFetchAccounts: new Dictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>
                {
                    [1] = Array.Empty<FetchAccountBackupAdministrationSnapshot>()
                }));
        Assert.IsNull(XDocument.Parse(emptyXml).Root!.Element("Domains")!.Element("Domain")!
            .Element("Accounts")!.Element("Account")!.Element("FetchAccounts"));
    }

    [TestMethod]
    public async Task PayloadRuntimeDoesNotLoadAliasesWhenDomainsAreNotSelected()
    {
        var aliasStore = new RecordingDomainAliasAdministrationStore(
            new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>());
        var normalAliasStore = new RecordingAliasAdministrationStore(
            new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>());
        var accountStore = new RecordingAccountAdministrationStore(
            new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>());
        var listStore = new RecordingDistributionListAdministrationStore(
            new Dictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>());
        var recipientStore = new RecordingDistributionListRecipientAdministrationStore(
            new Dictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>());
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(Array.Empty<DomainAdministrationSnapshot>()),
            aliasStore,
            accountStore,
            normalAliasStore,
            listStore,
            recipientStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 1,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<int>(), aliasStore.RequestedDomainIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), normalAliasStore.RequestedDomainIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), accountStore.RequestedDomainIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), listStore.RequestedDomainIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), recipientStore.RequestedListIds.ToArray());
        Assert.IsNull(payload.Domains);
        Assert.IsNull(payload.DomainAliases);
        Assert.IsNull(payload.Accounts);
        Assert.IsNull(payload.Aliases);
        Assert.IsNull(payload.DistributionLists);
        Assert.IsNull(payload.DistributionListRecipients);
    }

    private sealed class FixedSettingsAdministrationStore : ISettingsAdministrationStore
    {
        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                new SettingsAdministrationSnapshot(
                    "mail.example.test",
                    "smtp",
                    "pop3",
                    "imap"));
    }

    private sealed class FixedDomainAdministrationStore(
        IReadOnlyList<DomainAdministrationSnapshot> domains) : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(domains);
    }

    private sealed class RecordingDomainAliasAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>> aliases)
        : IDomainAliasAdministrationStore
    {
        public List<int> RequestedDomainIds { get; } = new();

        public ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            RequestedDomainIds.Add(domainId);
            return ValueTask.FromResult(
                aliases.TryGetValue(domainId, out var domainAliases)
                    ? domainAliases
                    : Array.Empty<DomainAliasAdministrationSnapshot>());
        }
    }

    private sealed class RecordingAliasAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<AliasAdministrationSnapshot>> aliases)
        : IAliasAdministrationStore
    {
        public List<int> RequestedDomainIds { get; } = new();

        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            RequestedDomainIds.Add(domainId);
            return ValueTask.FromResult(
                aliases.TryGetValue(domainId, out var domainAliases)
                    ? domainAliases
                    : Array.Empty<AliasAdministrationSnapshot>());
        }
    }

    private sealed class RecordingAccountAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<AccountAdministrationSnapshot>> accounts)
        : IAccountAdministrationStore
    {
        public List<int> RequestedDomainIds { get; } = new();

        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            RequestedDomainIds.Add(domainId);
            return ValueTask.FromResult(
                accounts.TryGetValue(domainId, out var domainAccounts)
                    ? domainAccounts
                    : Array.Empty<AccountAdministrationSnapshot>());
        }

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                accounts.Values
                    .SelectMany(static domainAccounts => domainAccounts)
                    .FirstOrDefault(account => account.Id == accountId));
    }

    private sealed class RecordingImapFolderAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>> folders)
        : IImapFolderAdministrationStore
    {
        public List<int> RequestedAccountIds { get; } = new();

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            RequestedAccountIds.Add(accountId);
            return ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                    folders.TryGetValue(accountId, out var accountFolders)
                        ? accountFolders
                        : Array.Empty<ImapFolderAdministrationSnapshot>());
        }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            RequestedAccountIds.Add(accountId);
            return ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                folders.TryGetValue(accountId, out var accountFolders)
                    ? accountFolders
                    : Array.Empty<ImapFolderAdministrationSnapshot>());
        }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
            int parentFolderId,
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>(
                Array.Empty<ImapFolderPermissionAdministrationSnapshot>());
    }

    private sealed class RecordingMessageAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>> messages)
        : IMessageAdministrationStore
    {
        public List<int> RequestedFolderIds { get; } = new();

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                Array.Empty<MessageAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
            int folderId,
            CancellationToken cancellationToken)
        {
            RequestedFolderIds.Add(folderId);
            return ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                messages.TryGetValue(folderId, out var folderMessages)
                    ? folderMessages
                    : Array.Empty<MessageAdministrationSnapshot>());
        }
    }

    private sealed class RecordingBackupAccountAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>> accounts)
        : IBackupAccountAdministrationStore
    {
        public List<int> RequestedDomainIds { get; } = new();

        public ValueTask<IReadOnlyList<AccountBackupAdministrationSnapshot>> GetBackupAccountsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            RequestedDomainIds.Add(domainId);
            return ValueTask.FromResult(
                accounts.TryGetValue(domainId, out var domainAccounts)
                    ? domainAccounts
                    : Array.Empty<AccountBackupAdministrationSnapshot>());
        }

    }

    private sealed class RecordingFetchAccountAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>> accounts)
        : IFetchAccountAdministrationStore
    {
        public List<int> RequestedAccountIds { get; } = new();

        public ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            RequestedAccountIds.Add(accountId);
            return ValueTask.FromResult(
                accounts.TryGetValue(accountId, out var fetchAccounts)
                    ? fetchAccounts
                    : Array.Empty<FetchAccountAdministrationSnapshot>());
        }

        public ValueTask SetRetryNowAsync(
            int accountId,
            int fetchAccountId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DeleteFetchAccountAsync(
            int accountId,
            int fetchAccountId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingBackupFetchAccountAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>> accounts)
        : IBackupFetchAccountAdministrationStore
    {
        public List<int> RequestedAccountIds { get; } = new();

        public ValueTask<IReadOnlyList<FetchAccountBackupAdministrationSnapshot>> GetBackupFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            RequestedAccountIds.Add(accountId);
            return ValueTask.FromResult(
                accounts.TryGetValue(accountId, out var fetchAccounts)
                    ? fetchAccounts
                    : Array.Empty<FetchAccountBackupAdministrationSnapshot>());
        }
    }

    private sealed class RecordingBackupRuleAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<RuleAdministrationSnapshot>> rules)
        : IBackupRuleAdministrationStore
    {
        public List<int> RequestedAccountIds { get; } = new();

        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetBackupRulesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            RequestedAccountIds.Add(accountId);
            return ValueTask.FromResult(
                rules.TryGetValue(accountId, out var accountRules)
                    ? accountRules
                    : Array.Empty<RuleAdministrationSnapshot>());
        }
    }

    private sealed class RecordingRuleCriteriaAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>> criteria)
        : IRuleCriteriaAdministrationStore
    {
        public List<int> RequestedRuleIds { get; } = new();

        public ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
            int ruleId,
            CancellationToken cancellationToken)
        {
            RequestedRuleIds.Add(ruleId);
            return ValueTask.FromResult(
                criteria.TryGetValue(ruleId, out var ruleCriteria)
                    ? ruleCriteria
                    : Array.Empty<RuleCriteriaAdministrationSnapshot>());
        }

        public ValueTask DeleteRuleCriteriaByIdAsync(
            int ruleId,
            int databaseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask SaveRuleCriteriaAsync(
            int owningRuleId,
            RuleCriteriaAdministrationSnapshot criterion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRuleActionAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>> actions)
        : IRuleActionAdministrationStore
    {
        public List<int> RequestedRuleIds { get; } = new();

        public ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
            int ruleId,
            CancellationToken cancellationToken)
        {
            RequestedRuleIds.Add(ruleId);
            return ValueTask.FromResult(
                actions.TryGetValue(ruleId, out var ruleActions)
                    ? ruleActions
                    : Array.Empty<RuleActionAdministrationSnapshot>());
        }

        public ValueTask DeleteRuleActionByIdAsync(
            int ruleId,
            int databaseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask SaveRuleActionAsync(
            int owningRuleId,
            RuleActionAdministrationSnapshot action,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static async Task<string> ReadMetadataXmlAsync(
        string sevenZipPath,
        string archivePath)
    {
        using var process = new Process
        {
            StartInfo = SevenZipBackupArchiveMetadataReader.CreateStartInfo(
                sevenZipPath,
                archivePath)
        };

        Assert.IsTrue(process.Start());
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        Assert.IsTrue(
            process.ExitCode is 0 or 1,
            $"7za metadata extraction failed: {error}");
        return output;
    }

    private static async Task ExtractArchiveAsync(
        string sevenZipPath,
        string archivePath,
        string destination)
    {
        Directory.CreateDirectory(destination);
        var startInfo = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add("-o" + destination);
        startInfo.ArgumentList.Add("-y");

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        Assert.IsTrue(
            process.ExitCode is 0 or 1,
            $"7za archive extraction failed: {output}\n{error}");
    }

    private static FetchAccountAdministrationSnapshot CreateFetchAccountSnapshot(
        int id,
        int accountId,
        string name) =>
        new(
            Id: id,
            AccountId: accountId,
            Name: name,
            ServerAddress: "pop3.example.test",
            Port: 995,
            ServerType: 0,
            Username: "user",
            MinutesBetweenFetch: 15,
            DaysToKeepMessages: 7,
            Enabled: true,
            ProcessMimeRecipients: true,
            ProcessMimeDate: true,
            ConnectionSecurity: 2,
            UseAntiSpam: true,
            UseAntiVirus: true,
            EnableRouteRecipients: true,
            MimeRecipientHeaders: "To",
            NextDownloadTime: "2026-07-30 01:02:03",
            IsLocked: false);

    private static RuleActionAdministrationSnapshot CreateRuleActionSnapshot(
        int id,
        int ruleId,
        string subject) =>
        new(
            Id: id,
            RuleId: ruleId,
            Type: 1,
            Subject: subject,
            Body: "body",
            FromName: "from-name",
            FromAddress: "from@example.test",
            Filename: "file",
            To: "to@example.test",
            ImapFolder: "INBOX",
            ScriptFunction: "script",
            HeaderName: "X-Test",
            Value: "value",
            RouteId: 0,
            AbortSpamFlagged: false,
            SortOrder: 1);

    private sealed class RecordingDistributionListAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>> lists)
        : IDistributionListAdministrationStore
    {
        public List<int> RequestedDomainIds { get; } = new();

        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            RequestedDomainIds.Add(domainId);
            return ValueTask.FromResult(
                lists.TryGetValue(domainId, out var domainLists)
                    ? domainLists
                    : Array.Empty<DistributionListAdministrationSnapshot>());
        }
    }

    private sealed class RecordingDistributionListRecipientAdministrationStore(
        IReadOnlyDictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> recipients)
        : IDistributionListRecipientAdministrationStore
    {
        public List<int> RequestedListIds { get; } = new();

        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken)
        {
            RequestedListIds.Add(distributionListId);
            return ValueTask.FromResult(
                recipients.TryGetValue(distributionListId, out var listRecipients)
                    ? listRecipients
                    : Array.Empty<DistributionListRecipientAdministrationSnapshot>());
        }
    }
}
