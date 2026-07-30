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
}
