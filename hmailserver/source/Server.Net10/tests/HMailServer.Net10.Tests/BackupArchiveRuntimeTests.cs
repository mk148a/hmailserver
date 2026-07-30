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
        Assert.IsNotNull(payload.DomainAliases);
        Assert.AreEqual(2, payload.DomainAliases.Count);
        CollectionAssert.AreEqual(
            new[] { "alias.alpha.example" },
            payload.DomainAliases[10].Select(static alias => alias.AliasName).ToArray());
        Assert.AreEqual(0, payload.DomainAliases[20].Count);
    }

    [TestMethod]
    public async Task PayloadRuntimeDoesNotLoadAliasesWhenDomainsAreNotSelected()
    {
        var aliasStore = new RecordingDomainAliasAdministrationStore(
            new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>());
        var runtime = new BackupXmlPayloadRuntime(
            new FixedSettingsAdministrationStore(),
            new FixedDomainAdministrationStore(Array.Empty<DomainAdministrationSnapshot>()),
            aliasStore);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                Destination: "backup",
                BackupOptions: 1,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<int>(), aliasStore.RequestedDomainIds.ToArray());
        Assert.IsNull(payload.Domains);
        Assert.IsNull(payload.DomainAliases);
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
}
