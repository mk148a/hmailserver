using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;
using System.Runtime.InteropServices;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerMessageIndexingIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesMessageIndexingAdministrationAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerMessageIndexingAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));
            MessageIndexingRuntimeHost.Configure(new StoreBackedMessageIndexingRuntime(store));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNull(application.Authenticate("Administrator", "wrong"));
            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var indexing = application.Settings.MessageIndexing;
            var extended = (IInterfaceMessageIndexing2)indexing;

            Assert.AreEqual(2, indexing.TotalMessageCount);
            Assert.AreEqual(1, indexing.TotalIndexedCount);
            Assert.IsFalse(indexing.Enabled);
            Assert.AreEqual("Queued=0", extended.BackfillStatus);

            indexing.Enabled = true;

            Assert.IsTrue(indexing.Enabled);
            Assert.AreEqual("Queued=1", extended.BackfillStatus);

            indexing.Clear();

            Assert.AreEqual(0, indexing.TotalIndexedCount);
            Assert.AreEqual("Queued=2", extended.BackfillStatus);

            indexing.Index();

            Assert.AreEqual("Queued=2", extended.BackfillStatus);
            indexing.Enabled = false;
            Assert.IsFalse(indexing.Enabled);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ReadsBoundedSettingsScalarsFromIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSettingsSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            SettingsAdministrationRuntimeHost.Configure(
                new SqlServerSettingsAdministrationStore(connectionFactory),
                new SettingsRuntimeConfiguration(
                    LoggingDirectory: @"C:\hMailServer\Logs",
                    ScriptingDirectory: @"C:\hMailServer\Events\"));
            BlockedAttachmentAdministrationRuntimeHost.Configure(
                new SqlServerBlockedAttachmentAdministrationStore(connectionFactory));
            DnsBlackListAdministrationRuntimeHost.Configure(
                new SqlServerDnsBlackListAdministrationStore(connectionFactory));
            SurblServerAdministrationRuntimeHost.Configure(
                new SqlServerSurblServerAdministrationStore(connectionFactory));
            GreyListingWhiteAddressAdministrationRuntimeHost.Configure(
                new SqlServerGreyListingWhiteAddressAdministrationStore(connectionFactory));
            WhiteListAddressAdministrationRuntimeHost.Configure(
                new SqlServerWhiteListAddressAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNull(application.Authenticate("Administrator", "wrong"));
            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var settings = application.Settings;

            Assert.AreEqual("mail.example.test", settings.HostName);
            Assert.AreEqual("SMTP ready", settings.WelcomeSMTP);
            Assert.AreEqual("POP3 ready", settings.WelcomePOP3);
            Assert.AreEqual("IMAP ready", settings.WelcomeIMAP);
            Assert.AreEqual(100, settings.MaxSMTPConnections);
            Assert.AreEqual(50, settings.MaxPOP3Connections);
            Assert.AreEqual(75, settings.MaxIMAPConnections);
            Assert.AreEqual(10, settings.MaxDeliveryThreads);
            Assert.IsTrue(settings.ServiceSMTP);
            Assert.IsFalse(settings.ServicePOP3);
            Assert.IsTrue(settings.ServiceIMAP);
            Assert.AreEqual(4, settings.SMTPNoOfTries);
            Assert.AreEqual(60, settings.SMTPMinutesBetweenTry);
            Assert.AreEqual("relay.example.test", settings.SMTPRelayer);
            Assert.IsTrue(settings.SMTPRelayerRequiresAuthentication);
            Assert.AreEqual("relay-user", settings.SMTPRelayerUsername);
            Assert.AreEqual(587, settings.SMTPRelayerPort);
            Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, settings.SMTPRelayerConnectionSecurity);
            Assert.AreEqual(ComConnectionSecurity.StartTlsOptional, settings.SMTPConnectionSecurity);
            Assert.IsTrue(settings.TlsVersion10Enabled);
            Assert.IsFalse(settings.TlsVersion11Enabled);
            Assert.IsTrue(settings.TlsVersion12Enabled);
            Assert.IsTrue(settings.TlsVersion13Enabled);
            Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);
            Assert.IsFalse(settings.TlsOptionPrioritizeChaChaEnabled);
            Assert.AreEqual("master-user", settings.IMAPMasterUser);
            Assert.AreEqual(15, settings.MaxAsynchronousThreads);
            var cache = settings.Cache;
            Assert.IsTrue(cache.Enabled);
            Assert.AreEqual(61, cache.DomainCacheTTL);
            Assert.AreEqual(62, cache.AccountCacheTTL);
            Assert.AreEqual(63, cache.AliasCacheTTL);
            Assert.AreEqual(64, cache.DistributionListCacheTTL);
            var pendingCacheRuntime = Assert.ThrowsExactly<COMException>(() => _ = cache.DomainHitRate);
            Assert.AreEqual(unchecked((int)0x80004001), pendingCacheRuntime.ErrorCode);
            var pendingCacheClear = Assert.ThrowsExactly<COMException>(cache.Clear);
            Assert.AreEqual(unchecked((int)0x80004001), pendingCacheClear.ErrorCode);
            var logging = settings.Logging;
            Assert.IsTrue(logging.Enabled);
            Assert.IsTrue(logging.LogSMTP);
            Assert.IsFalse(logging.LogPOP3);
            Assert.IsTrue(logging.LogTCPIP);
            Assert.IsTrue(logging.LogApplication);
            Assert.AreEqual(ComLogDevice.File, logging.Device);
            Assert.AreEqual(ComLogOutputFormat.Csa, logging.LogFormat);
            Assert.IsTrue(logging.LogDebug);
            Assert.IsTrue(logging.LogIMAP);
            Assert.IsTrue(logging.AWStatsEnabled);
            Assert.IsTrue(logging.KeepFilesOpen);
            var scripting = settings.Scripting;
            Assert.IsTrue(scripting.Enabled);
            Assert.AreEqual("JScript", scripting.Language);
            Assert.AreEqual(@"C:\hMailServer\Events\", scripting.Directory);
            Assert.AreEqual(@"C:\hMailServer\Events\\EventHandlers.js", scripting.CurrentScriptFile);
            var backup = settings.Backup;
            Assert.AreEqual(@"D:\hMailServer Backup", backup.Destination);
            Assert.IsTrue(backup.BackupSettings);
            Assert.IsFalse(backup.BackupDomains);
            Assert.IsTrue(backup.BackupMessages);
            Assert.IsTrue(backup.CompressDestinationFiles);
            Assert.AreEqual(@"C:\hMailServer\Logs\hmailserver_backup.log", backup.LogFile);
            var antiVirus = settings.AntiVirus;
            Assert.IsTrue(antiVirus.ClamWinEnabled);
            Assert.AreEqual(@"C:\ClamWin\bin\clamscan.exe", antiVirus.ClamWinExecutable);
            Assert.AreEqual(@"C:\ClamWin\db", antiVirus.ClamWinDBFolder);
            Assert.AreEqual(ComAntivirusAction.DeleteAttachments, antiVirus.Action);
            Assert.IsTrue(antiVirus.NotifyReceiver);
            Assert.IsFalse(antiVirus.NotifySender);
            Assert.IsTrue(antiVirus.CustomScannerEnabled);
            Assert.AreEqual(@"C:\Tools\virus-scan.cmd", antiVirus.CustomScannerExecutable);
            Assert.AreEqual(7, antiVirus.CustomScannerReturnValue);
            Assert.AreEqual(4096, antiVirus.MaximumMessageSize);
            Assert.IsTrue(antiVirus.EnableAttachmentBlocking);
            Assert.IsTrue(antiVirus.ClamAVEnabled);
            Assert.AreEqual("127.0.0.1", antiVirus.ClamAVHost);
            Assert.AreEqual(3310, antiVirus.ClamAVPort);
            var blockedAttachments = antiVirus.BlockedAttachments;
            Assert.AreEqual(2, blockedAttachments.Count);
            Assert.AreEqual("*.bat", blockedAttachments[0].Wildcard);
            Assert.AreEqual("Batch file", blockedAttachments[0].Description);
            Assert.AreEqual("*.exe", blockedAttachments.get_ItemByDBID(20).Wildcard);
            var pendingBlockedAttachmentSave = Assert.ThrowsExactly<COMException>(blockedAttachments[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingBlockedAttachmentSave.ErrorCode);
            var antiSpam = settings.AntiSpam;
            Assert.IsTrue(antiSpam.GreyListingEnabled);
            Assert.AreEqual(30, antiSpam.GreyListingInitialDelay);
            Assert.AreEqual(48, antiSpam.GreyListingInitialDelete);
            Assert.AreEqual(864, antiSpam.GreyListingFinalDelete);
            Assert.IsTrue(antiSpam.CheckHostInHelo);
            Assert.AreEqual(2, antiSpam.CheckHostInHeloScore);
            Assert.IsTrue(antiSpam.CheckPTR);
            Assert.AreEqual(4, antiSpam.CheckPTRScore);
            Assert.IsTrue(antiSpam.AddHeaderSpam);
            Assert.IsFalse(antiSpam.AddHeaderReason);
            Assert.IsTrue(antiSpam.PrependSubject);
            Assert.AreEqual("[SPAM]", antiSpam.PrependSubjectText);
            Assert.AreEqual(5, antiSpam.SpamMarkThreshold);
            Assert.AreEqual(20, antiSpam.SpamDeleteThreshold);
            Assert.IsTrue(antiSpam.UseSPF);
            Assert.AreEqual(3, antiSpam.UseSPFScore);
            Assert.IsTrue(antiSpam.UseMXChecks);
            Assert.AreEqual(6, antiSpam.UseMXChecksScore);
            Assert.IsTrue(antiSpam.SpamAssassinEnabled);
            Assert.AreEqual(7, antiSpam.SpamAssassinScore);
            Assert.IsFalse(antiSpam.SpamAssassinMergeScore);
            Assert.AreEqual("spamd.example.test", antiSpam.SpamAssassinHost);
            Assert.AreEqual(783, antiSpam.SpamAssassinPort);
            Assert.AreEqual(1024, antiSpam.MaximumMessageSize);
            Assert.IsTrue(antiSpam.DKIMVerificationEnabled);
            Assert.AreEqual(8, antiSpam.DKIMVerificationFailureScore);
            Assert.IsTrue(antiSpam.BypassGreylistingOnSPFSuccess);
            Assert.IsFalse(antiSpam.BypassGreylistingOnMailFromMX);
            var dnsBlackLists = antiSpam.DNSBlackLists;
            Assert.AreEqual(2, dnsBlackLists.Count);
            Assert.AreEqual(10, dnsBlackLists[0].ID);
            Assert.IsTrue(dnsBlackLists[0].Active);
            Assert.AreEqual("zen.spamhaus.org", dnsBlackLists[0].DNSHost);
            Assert.AreEqual("Rejected by Spamhaus.", dnsBlackLists[0].RejectMessage);
            Assert.AreEqual("127.0.0.2-8|127.0.0.10-11", dnsBlackLists[0].ExpectedResult);
            Assert.AreEqual(4, dnsBlackLists[0].Score);
            Assert.IsFalse(dnsBlackLists.get_ItemByDBID(20).Active);
            Assert.AreEqual(20, dnsBlackLists.get_ItemByDNSHost("BL.SPAMCOP.NET").ID);
            var pendingDnsBlackListSave = Assert.ThrowsExactly<COMException>(dnsBlackLists[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingDnsBlackListSave.ErrorCode);
            var surblServers = antiSpam.SURBLServers;
            Assert.AreEqual(2, surblServers.Count);
            Assert.AreEqual(10, surblServers[0].ID);
            Assert.IsTrue(surblServers[0].Active);
            Assert.AreEqual("multi.surbl.org", surblServers[0].DNSHost);
            Assert.AreEqual("Rejected by SURBL.", surblServers[0].RejectMessage);
            Assert.AreEqual(4, surblServers[0].Score);
            Assert.IsFalse(surblServers.get_ItemByDBID(20).Active);
            Assert.AreEqual(20, surblServers.get_ItemByDNSHost("EXAMPLE.SURBL.TEST").ID);
            var pendingSurblServerSave = Assert.ThrowsExactly<COMException>(surblServers[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingSurblServerSave.ErrorCode);
            var greyListingWhiteAddresses = antiSpam.GreyListingWhiteAddresses;
            Assert.AreEqual(2, greyListingWhiteAddresses.Count);
            Assert.AreEqual(10, greyListingWhiteAddresses[0].ID);
            Assert.AreEqual("192.0.2.*", greyListingWhiteAddresses[0].IPAddress);
            Assert.AreEqual("Test network", greyListingWhiteAddresses[0].Description);
            Assert.AreEqual(20, greyListingWhiteAddresses.get_ItemByDBID(20).ID);
            Assert.AreEqual(10, greyListingWhiteAddresses.get_ItemByName("192.0.2.%").ID);
            var pendingGreyListingWhiteAddressSave = Assert.ThrowsExactly<COMException>(
                greyListingWhiteAddresses[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingGreyListingWhiteAddressSave.ErrorCode);
            var whiteListAddresses = antiSpam.WhiteListAddresses;
            Assert.AreEqual(2, whiteListAddresses.Count);
            Assert.AreEqual(10, whiteListAddresses[0].ID);
            Assert.AreEqual("192.0.2.1", whiteListAddresses[0].LowerIPAddress);
            Assert.AreEqual("192.0.2.255", whiteListAddresses[0].UpperIPAddress);
            Assert.AreEqual("*@example.test", whiteListAddresses[0].EmailAddress);
            Assert.AreEqual("Test network", whiteListAddresses[0].Description);
            Assert.AreEqual(20, whiteListAddresses.get_ItemByDBID(20).ID);
            var pendingWhiteListAddressSave = Assert.ThrowsExactly<COMException>(whiteListAddresses[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingWhiteListAddressSave.ErrorCode);
            var pendingSpamAssassinTest = Assert.ThrowsExactly<COMException>(
                () => antiSpam.TestSpamAssassinConnection("127.0.0.1", 783, out _));
            Assert.AreEqual(unchecked((int)0x80004001), pendingSpamAssassinTest.ErrorCode);
            Assert.AreEqual(20480, settings.MaxMessageSize);
            Assert.AreEqual(100, settings.MaxSMTPRecipientsInBatch);
            Assert.IsTrue(settings.DisconnectInvalidClients);
            Assert.AreEqual(12, settings.MaxNumberOfInvalidCommands);
            Assert.IsTrue(settings.IMAPSortEnabled);
            Assert.IsFalse(settings.IMAPQuotaEnabled);
            Assert.IsTrue(settings.IMAPIdleEnabled);
            Assert.IsFalse(settings.IMAPACLEnabled);
            Assert.IsTrue(settings.IMAPSASLPlainEnabled);
            Assert.IsFalse(settings.IMAPSASLInitialResponseEnabled);
            Assert.AreEqual("#Shared", settings.IMAPPublicFolderName);
            Assert.AreEqual("/", settings.IMAPHierarchyDelimiter);
            Assert.IsTrue(settings.AllowSMTPAuthPlain);
            Assert.IsTrue(settings.DenyMailFromNull);
            Assert.IsTrue(settings.AllowIncorrectLineEndings);
            Assert.IsFalse(settings.AddDeliveredToHeader);
            Assert.AreEqual("archive@example.test", settings.MirrorEMailAddress);
            Assert.AreEqual("example.test", settings.DefaultDomain);
            Assert.AreEqual("192.0.2.25", settings.SMTPDeliveryBindToIP);
            Assert.AreEqual(9, settings.RuleLoopLimit);
            Assert.AreEqual(-1, settings.WorkerThreadPriority);
            Assert.AreEqual(16, settings.TCPIPThreads);
            Assert.AreEqual(22, settings.MaxNumberOfMXHosts);
            Assert.IsTrue(settings.VerifyRemoteSslCertificate);
            Assert.AreEqual("TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256", settings.SslCipherList);
            Assert.IsTrue(settings.IPv6PreferredEnabled);
            Assert.IsTrue(settings.AutoBanOnLogonFailure);
            Assert.AreEqual(3, settings.MaxInvalidLogonAttempts);
            Assert.AreEqual(30, settings.MaxInvalidLogonAttemptsWithin);
            Assert.AreEqual(60, settings.AutoBanMinutes);
        }
        finally
        {
            SettingsAdministrationRuntimeHost.Configure(new FixedSettingsAdministrationStore());
            BlockedAttachmentAdministrationRuntimeHost.Configure(
                new FixedBlockedAttachmentAdministrationStore(Array.Empty<BlockedAttachmentAdministrationSnapshot>()));
            DnsBlackListAdministrationRuntimeHost.Configure(
                new FixedDnsBlackListAdministrationStore(Array.Empty<DnsBlackListAdministrationSnapshot>()));
            SurblServerAdministrationRuntimeHost.Configure(
                new FixedSurblServerAdministrationStore(Array.Empty<SurblServerAdministrationSnapshot>()));
            GreyListingWhiteAddressAdministrationRuntimeHost.Configure(
                new FixedGreyListingWhiteAddressAdministrationStore(
                    Array.Empty<GreyListingWhiteAddressAdministrationSnapshot>()));
            WhiteListAddressAdministrationRuntimeHost.Configure(
                new FixedWhiteListAddressAdministrationStore(Array.Empty<WhiteListAddressAdministrationSnapshot>()));
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ResetsAndRemovesOnlyEligibleDeliveryQueueRowsAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-net10-queue-admin-" + Guid.NewGuid().ToString("N"));
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(dataDirectory);
            foreach (var fileName in new[]
                     {
                         "active.eml",
                         "delivered.eml",
                         "ready.eml",
                         "expired.eml",
                         "clear-a.eml",
                         "clear-b.eml"
                     })
            {
                await File.WriteAllTextAsync(
                    Path.Combine(dataDirectory, fileName),
                    "Subject: Queue administration\r\n\r\nBody\r\n").ConfigureAwait(false);
            }

            await CreateDeliveryQueueAdministrationSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var administrationStore = new SqlServerDeliveryQueueAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString),
                new MessageFilePathResolver(
                    new MessageFileSearchDocumentSourceOptions(dataDirectory)));
            var clearObserver = new IntegrationDeliveryQueueClearObserver();
            DeliveryQueueAdministrationRuntimeHost.Configure(
                administrationStore,
                clearCoordinator: new DeliveryQueueClearCoordinator(
                    new DeliveryQueueClearOptions(BatchSize: 1),
                    administrationStore,
                    clearObserver,
                    CancellationToken.None));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var queue = application.GlobalObjects.DeliveryQueue;

            queue.ResetDeliveryTime(10);
            queue.ResetDeliveryTime(20);
            queue.ResetDeliveryTime(999);
            queue.Remove(30);
            queue.Remove(40);
            queue.Remove(10);
            queue.Remove(20);
            queue.Remove(999);
            queue.Clear();

            Assert.AreEqual(
                2,
                await clearObserver.Completion.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false));

            await using var connection = new SqlConnection(testConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new SqlCommand(
                """
SELECT
    messageid,
    messagetype,
    messagenexttrytime,
    messagecurnooftries,
    messagelocked,
    messageleaseowner,
    messageleaseexpiresutc
FROM hm_messages
ORDER BY messageid;
""",
                connection);
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
            Assert.AreEqual(10L, reader.GetInt64(0));
            Assert.AreEqual(1, reader.GetInt32(1));
            var resetTime = reader.GetDateTime(2);
            Assert.IsTrue(resetTime <= DateTime.UtcNow);
            Assert.IsTrue(resetTime >= DateTime.UtcNow.AddMinutes(-5));
            Assert.AreEqual(7, reader.GetInt32(3));
            Assert.AreEqual(1, reader.GetInt32(4));
            Assert.AreEqual("worker-a", reader.GetString(5));
            Assert.AreEqual(new DateTime(2099, 1, 1), reader.GetDateTime(6));

            Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
            Assert.AreEqual(20L, reader.GetInt64(0));
            Assert.AreEqual(2, reader.GetInt32(1));
            Assert.AreEqual(new DateTime(2099, 1, 1), reader.GetDateTime(2));
            Assert.AreEqual(9, reader.GetInt32(3));
            Assert.AreEqual(0, reader.GetInt32(4));
            Assert.IsTrue(reader.IsDBNull(5));
            Assert.IsTrue(reader.IsDBNull(6));
            Assert.IsFalse(await reader.ReadAsync().ConfigureAwait(false));
            await reader.DisposeAsync().ConfigureAwait(false);

            await using var recipientCommand = new SqlCommand(
                "SELECT COUNT(*) FROM hm_messagerecipients;",
                connection);
            Assert.AreEqual(
                2,
                Convert.ToInt32(
                    await recipientCommand.ExecuteScalarAsync().ConfigureAwait(false),
                    System.Globalization.CultureInfo.InvariantCulture));

            Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, "active.eml")));
            Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, "delivered.eml")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "ready.eml")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "expired.eml")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "clear-a.eml")));
            Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "clear-b.eml")));
        }
        finally
        {
            DeliveryQueueAdministrationRuntimeHost.ResetForTests();
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesDomainLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            DomainAdministrationRuntimeHost.Configure(
                new SqlServerDomainAdministrationStore(
                    new SqlServerConnectionFactory(testConnectionString)));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domains = application.Domains;

            Assert.AreEqual(2, domains.Count);
            Assert.AreEqual("10\talpha.example\t1\r\n20\tbeta.example\t0\r\n", domains.Names);
            Assert.AreEqual("alpha.example", domains[0].Name);
            Assert.AreEqual("postmaster@alpha.example", domains[0].Postmaster);
            Assert.AreEqual(1024, domains[0].MaxMessageSize);
            Assert.IsTrue(domains[0].PlusAddressingEnabled);
            Assert.AreEqual("+", domains[0].PlusAddressingCharacter);
            Assert.IsTrue(domains[0].AntiSpamEnableGreylisting);
            Assert.AreEqual("corp.alpha.example", domains[0].ADDomainName);
            Assert.AreEqual(4096, domains[0].MaxSize);
            Assert.AreEqual(2, domains[0].Size);
            Assert.AreEqual(3072L, domains[0].AllocatedSize);
            Assert.AreEqual(200, domains[0].MaxNumberOfAccounts);
            Assert.AreEqual(30, domains[0].MaxNumberOfAliases);
            Assert.AreEqual(12, domains[0].MaxNumberOfDistributionLists);
            Assert.IsTrue(domains[0].MaxNumberOfAccountsEnabled);
            Assert.IsFalse(domains[0].MaxNumberOfAliasesEnabled);
            Assert.IsTrue(domains[0].MaxNumberOfDistributionListsEnabled);
            Assert.AreEqual(512, domains[0].MaxAccountSize);
            Assert.IsTrue(domains[0].SignatureEnabled);
            Assert.AreEqual(ComDomainSignatureMethod.AppendToAccountSignature, domains[0].SignatureMethod);
            Assert.AreEqual("Alpha plain signature", domains[0].SignaturePlainText);
            Assert.AreEqual("<p>Alpha HTML signature</p>", domains[0].SignatureHTML);
            Assert.IsTrue(domains[0].AddSignaturesToReplies);
            Assert.IsFalse(domains[0].AddSignaturesToLocalMail);
            Assert.IsTrue(domains[0].DKIMSignEnabled);
            Assert.AreEqual("alpha-selector", domains[0].DKIMSelector);
            Assert.AreEqual(@"C:\keys\alpha.pem", domains[0].DKIMPrivateKeyFile);
            Assert.AreEqual(ComDkimCanonicalizationMethod.Simple, domains[0].DKIMHeaderCanonicalizationMethod);
            Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, domains[0].DKIMBodyCanonicalizationMethod);
            Assert.AreEqual(ComDkimAlgorithm.SHA1, domains[0].DKIMSigningAlgorithm);
            Assert.IsTrue(domains[0].DKIMSignAliasesEnabled);
            Assert.AreEqual("beta.example", domains.get_ItemByName("BETA.EXAMPLE").Name);
            Assert.IsFalse(domains.get_ItemByDBID(20).Active);
            Assert.IsFalse(domains.get_ItemByDBID(20).AntiSpamEnableGreylisting);
            Assert.AreEqual(string.Empty, domains.get_ItemByDBID(20).ADDomainName);
            Assert.AreEqual(1, domains.get_ItemByDBID(20).Size);
            Assert.AreEqual(128L, domains.get_ItemByDBID(20).AllocatedSize);
            Assert.IsFalse(domains.get_ItemByDBID(20).SignatureEnabled);
            Assert.AreEqual(
                ComDomainSignatureMethod.SetIfNotSpecifiedInAccount,
                domains.get_ItemByDBID(20).SignatureMethod);
            Assert.AreEqual(string.Empty, domains.get_ItemByDBID(20).SignaturePlainText);
            Assert.AreEqual(string.Empty, domains.get_ItemByDBID(20).SignatureHTML);
            Assert.IsFalse(domains.get_ItemByDBID(20).AddSignaturesToReplies);
            Assert.IsTrue(domains.get_ItemByDBID(20).AddSignaturesToLocalMail);
            Assert.IsFalse(domains.get_ItemByDBID(20).DKIMSignEnabled);
            Assert.AreEqual(string.Empty, domains.get_ItemByDBID(20).DKIMSelector);
            Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, domains.get_ItemByDBID(20).DKIMHeaderCanonicalizationMethod);
            Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, domains.get_ItemByDBID(20).DKIMBodyCanonicalizationMethod);
            Assert.AreEqual(ComDkimAlgorithm.SHA256, domains.get_ItemByDBID(20).DKIMSigningAlgorithm);
            Assert.IsFalse(domains.get_ItemByDBID(20).DKIMSignAliasesEnabled);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesAccountLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndAccountSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            AccountAdministrationRuntimeHost.Configure(new SqlServerAccountAdministrationStore(connectionFactory));
            MessageAdministrationRuntimeHost.Configure(new SqlServerMessageAdministrationStore(connectionFactory));
            FetchAccountAdministrationRuntimeHost.Configure(new SqlServerFetchAccountAdministrationStore(connectionFactory));
            RuleAdministrationRuntimeHost.Configure(new SqlServerRuleAdministrationStore(connectionFactory));
            RuleCriteriaAdministrationRuntimeHost.Configure(
                new SqlServerRuleCriteriaAdministrationStore(connectionFactory));
            RuleActionAdministrationRuntimeHost.Configure(
                new SqlServerRuleActionAdministrationStore(connectionFactory));
            ImapFolderAdministrationRuntimeHost.Configure(new SqlServerImapFolderAdministrationStore(connectionFactory));
            RouteAdministrationRuntimeHost.Configure(new SqlServerRouteAdministrationStore(connectionFactory));
            RouteAddressAdministrationRuntimeHost.Configure(
                new SqlServerRouteAddressAdministrationStore(connectionFactory));
            IncomingRelayAdministrationRuntimeHost.Configure(
                new SqlServerIncomingRelayAdministrationStore(connectionFactory));
            SecurityRangeAdministrationRuntimeHost.Configure(
                new SqlServerSecurityRangeAdministrationStore(connectionFactory));
            TcpIpPortAdministrationRuntimeHost.Configure(
                new SqlServerTcpIpPortAdministrationStore(connectionFactory));
            SslCertificateAdministrationRuntimeHost.Configure(
                new SqlServerSslCertificateAdministrationStore(connectionFactory));
            ServerMessageAdministrationRuntimeHost.Configure(
                new SqlServerServerMessageAdministrationStore(connectionFactory));
            GroupAdministrationRuntimeHost.Configure(
                new SqlServerGroupAdministrationStore(connectionFactory));
            GroupMemberAdministrationRuntimeHost.Configure(
                new SqlServerGroupMemberAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domain = application.Domains.get_ItemByName("example.test");
            var accounts = domain.Accounts;

            Assert.AreEqual(2, accounts.Count);
            Assert.AreEqual("admin@example.test", accounts[0].Address);
            Assert.AreEqual(10, accounts[0].DomainID);
            Assert.AreEqual(ComAdminLevel.ServerAdministrator, accounts[0].AdminLevel);
            Assert.IsTrue(accounts[0].IsAD);
            Assert.AreEqual("corp.example.test", accounts[0].ADDomain);
            Assert.AreEqual("ada.lovelace", accounts[0].ADUsername);
            Assert.AreEqual(2, accounts[0].MaxSize);
            Assert.AreEqual(2.5f, accounts[0].Size, 0.0001f);
            Assert.AreEqual(125, accounts[0].QuotaUsed);
            Assert.AreEqual(new DateTime(2026, 3, 4, 5, 6, 7), accounts[0].LastLogonTime);
            Assert.AreEqual("Ada", accounts[0].PersonFirstName);
            Assert.AreEqual("Lovelace", accounts[0].PersonLastName);
            Assert.IsTrue(accounts[0].VacationMessageIsOn);
            Assert.AreEqual("Away until Monday", accounts[0].VacationMessage);
            Assert.AreEqual("Auto reply", accounts[0].VacationSubject);
            Assert.IsTrue(accounts[0].VacationMessageExpires);
            Assert.AreEqual("2026-12-31", accounts[0].VacationMessageExpiresDate);
            Assert.IsTrue(accounts[0].VacationMessageAbortSpamFlagged);
            Assert.IsTrue(accounts[0].ForwardEnabled);
            Assert.AreEqual("archive@example.test", accounts[0].ForwardAddress);
            Assert.IsTrue(accounts[0].ForwardKeepOriginal);
            Assert.IsTrue(accounts[0].ForwardAbortSpamFlagged);
            Assert.IsTrue(accounts[0].SignatureEnabled);
            Assert.AreEqual("Regards,\r\nAda", accounts[0].SignaturePlainText);
            Assert.AreEqual("<p>Regards,<br>Ada</p>", accounts[0].SignatureHTML);
            var fetchAccounts = accounts[0].FetchAccounts;
            Assert.AreEqual(1, fetchAccounts.Count);
            Assert.AreEqual(10, fetchAccounts[0].AccountID);
            Assert.AreEqual("External POP3", fetchAccounts[0].Name);
            Assert.AreEqual("pop3.example.test", fetchAccounts[0].ServerAddress);
            Assert.AreEqual(995, fetchAccounts[0].Port);
            Assert.AreEqual("external-user", fetchAccounts[0].Username);
            Assert.AreEqual(ComConnectionSecurity.Tls, fetchAccounts[0].ConnectionSecurity);
            Assert.IsTrue(fetchAccounts[0].UseSSL);
            Assert.AreEqual("2026-07-01 02:03:04", fetchAccounts[0].NextDownloadTime);
            var pendingSensitiveRead = Assert.ThrowsExactly<COMException>(() => _ = fetchAccounts[0].Password);
            Assert.AreEqual(unchecked((int)0x80004001), pendingSensitiveRead.ErrorCode);
            var adminMessages = accounts[0].Messages;
            Assert.AreEqual(1, adminMessages.Count);
            Assert.AreEqual(3000L, adminMessages[0].ID);
            Assert.AreEqual("admin-inbox.eml", adminMessages[0].Filename);
            Assert.AreEqual("sender@example.test", adminMessages[0].FromAddress);
            Assert.AreEqual(2, adminMessages[0].State);
            Assert.AreEqual(2560, adminMessages[0].Size);
            Assert.AreEqual(3, adminMessages[0].DeliveryAttempt);
            Assert.AreEqual(41, adminMessages[0].UID);
            Assert.AreEqual(new DateTime(2026, 7, 1, 1, 2, 3), adminMessages[0].InternalDate);
            Assert.IsTrue(adminMessages[0].get_Flag(ComMessageFlag.Seen));
            Assert.IsTrue(adminMessages[0].get_Flag(ComMessageFlag.Recent));
            Assert.IsFalse(adminMessages[0].get_Flag(ComMessageFlag.Deleted));
            var pendingMessageBody = Assert.ThrowsExactly<COMException>(() => _ = adminMessages[0].Body);
            Assert.AreEqual(unchecked((int)0x80004001), pendingMessageBody.ErrorCode);
            Assert.AreEqual(0.125f, accounts.get_ItemByDBID(20).Size, 0.0001f);
            Assert.AreEqual(0, accounts.get_ItemByDBID(20).QuotaUsed);
            Assert.AreEqual(new DateTime(2026, 2, 3, 4, 5, 6), accounts.get_ItemByDBID(20).LastLogonTime);
            Assert.IsFalse(accounts.get_ItemByDBID(20).IsAD);
            Assert.AreEqual(string.Empty, accounts.get_ItemByDBID(20).ADDomain);
            Assert.AreEqual(string.Empty, accounts.get_ItemByDBID(20).ADUsername);
            var rules = accounts[0].Rules;
            Assert.AreEqual(2, rules.Count);
            Assert.AreEqual("First rule", rules[0].Name);
            Assert.AreEqual(10, rules[0].AccountID);
            Assert.IsTrue(rules[0].Active);
            Assert.IsTrue(rules[0].UseAND);
            Assert.AreEqual("Second rule", rules.get_ItemByDBID(300).Name);
            Assert.IsFalse(rules.get_ItemByDBID(300).Active);
            var firstRuleCriteria = rules[0].Criterias;
            Assert.AreEqual(2, firstRuleCriteria.Count);
            Assert.AreEqual(2000, firstRuleCriteria[0].ID);
            Assert.AreEqual(200, firstRuleCriteria[0].RuleID);
            Assert.IsTrue(firstRuleCriteria[0].UsePredefined);
            Assert.AreEqual(ComRulePredefinedField.Subject, firstRuleCriteria[0].PredefinedField);
            Assert.AreEqual(ComRuleMatchType.Contains, firstRuleCriteria[0].MatchType);
            Assert.AreEqual("invoice", firstRuleCriteria[0].MatchValue);
            Assert.AreEqual(string.Empty, firstRuleCriteria[0].HeaderField);
            Assert.AreEqual("X-Priority", firstRuleCriteria.get_ItemByDBID(2001).HeaderField);
            var outsideRuleCriterion = Assert.ThrowsExactly<COMException>(
                () => _ = firstRuleCriteria.get_ItemByDBID(3000));
            Assert.AreEqual(unchecked((int)0x8002000B), outsideRuleCriterion.ErrorCode);
            var pendingRuleCriterionSave = Assert.ThrowsExactly<COMException>(firstRuleCriteria[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingRuleCriterionSave.ErrorCode);
            var firstRuleActions = rules[0].Actions;
            Assert.AreEqual(2, firstRuleActions.Count);
            Assert.AreEqual(20000, firstRuleActions[0].ID);
            Assert.AreEqual(200, firstRuleActions[0].RuleID);
            Assert.AreEqual(ComRuleActionType.Reply, firstRuleActions[0].Type);
            Assert.AreEqual("Invoice received", firstRuleActions[0].Subject);
            Assert.AreEqual("Thank you", firstRuleActions[0].Body);
            Assert.AreEqual("Billing", firstRuleActions[0].FromName);
            Assert.AreEqual("billing@example.test", firstRuleActions[0].FromAddress);
            Assert.AreEqual("reply.eml", firstRuleActions[0].Filename);
            Assert.AreEqual("sender@example.test", firstRuleActions[0].To);
            Assert.AreEqual("Processed", firstRuleActions[0].IMAPFolder);
            Assert.AreEqual("HandleInvoice", firstRuleActions[0].ScriptFunction);
            Assert.AreEqual("X-Processed", firstRuleActions[0].HeaderName);
            Assert.AreEqual("yes", firstRuleActions[0].Value);
            Assert.AreEqual(500, firstRuleActions[0].RouteID);
            Assert.IsTrue(firstRuleActions[0].AbortSpamFlagged);
            Assert.AreEqual(ComRuleActionType.SendUsingRoute, firstRuleActions.get_ItemByDBID(20001).Type);
            var outsideRuleAction = Assert.ThrowsExactly<COMException>(
                () => _ = firstRuleActions.get_ItemByDBID(30000));
            Assert.AreEqual(unchecked((int)0x8002000B), outsideRuleAction.ErrorCode);
            var pendingRuleActionSave = Assert.ThrowsExactly<COMException>(firstRuleActions[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingRuleActionSave.ErrorCode);
            var globalRules = application.Rules;
            Assert.AreEqual(2, globalRules.Count);
            Assert.AreEqual("Global first", globalRules[0].Name);
            Assert.AreEqual(0, globalRules[0].AccountID);
            Assert.IsTrue(globalRules[0].Active);
            Assert.IsTrue(globalRules[0].UseAND);
            Assert.AreEqual("Global second", globalRules.get_ItemByDBID(150).Name);
            Assert.IsFalse(globalRules.get_ItemByDBID(150).Active);
            var globalRuleCriteria = globalRules[0].Criterias;
            Assert.AreEqual(1, globalRuleCriteria.Count);
            Assert.AreEqual(1000, globalRuleCriteria[0].ID);
            Assert.AreEqual(100, globalRuleCriteria[0].RuleID);
            var globalRuleActions = globalRules[0].Actions;
            Assert.AreEqual(1, globalRuleActions.Count);
            Assert.AreEqual(10000, globalRuleActions[0].ID);
            Assert.AreEqual(ComRuleActionType.SetHeaderValue, globalRuleActions[0].Type);
            var folders = accounts[0].IMAPFolders;
            Assert.AreEqual(2, folders.Count);
            Assert.AreEqual(100, folders[0].ID);
            Assert.AreEqual(-1, folders[0].ParentID);
            Assert.AreEqual("Inbox", folders[0].Name);
            Assert.IsTrue(folders[0].Subscribed);
            Assert.AreEqual(42, folders[0].CurrentUID);
            Assert.AreEqual("2026-06-27 01:02:03", folders[0].CreationTime);
            Assert.AreEqual(300, folders.get_ItemByName("teåäöst").ID);
            Assert.AreEqual("TEåäöST", folders.get_ItemByDBID(300).Name);
            var nestedFolder = Assert.ThrowsExactly<COMException>(() => _ = folders.get_ItemByDBID(200));
            Assert.AreEqual(unchecked((int)0x8002000B), nestedFolder.ErrorCode);
            var subFolders = folders[0].SubFolders;
            Assert.AreEqual(1, subFolders.Count);
            Assert.AreEqual(200, subFolders[0].ID);
            Assert.AreEqual(100, subFolders[0].ParentID);
            Assert.AreEqual("Child", subFolders[0].Name);
            Assert.AreEqual(3, subFolders[0].CurrentUID);
            var rootFolderFromChildCollection = Assert.ThrowsExactly<COMException>(
                () => _ = subFolders.get_ItemByDBID(300));
            Assert.AreEqual(unchecked((int)0x8002000B), rootFolderFromChildCollection.ErrorCode);
            var folderMessages = folders[0].Messages;
            Assert.AreEqual(1, folderMessages.Count);
            Assert.AreEqual(3000L, folderMessages[0].ID);
            Assert.AreEqual(3000L, folderMessages.get_ItemByDBID(3000).ID);
            var outsideFolderMessage = Assert.ThrowsExactly<COMException>(() => _ = folderMessages.get_ItemByDBID(3001));
            Assert.AreEqual(unchecked((int)0x8002000B), outsideFolderMessage.ErrorCode);
            var pendingMessagesDelete = Assert.ThrowsExactly<COMException>(() => folderMessages.DeleteByDBID(3000));
            Assert.AreEqual(unchecked((int)0x80004001), pendingMessagesDelete.ErrorCode);
            var privateFolderPermissions = Assert.ThrowsExactly<COMException>(() => _ = folders[0].Permissions);
            Assert.AreEqual(unchecked((int)0x800403E9), privateFolderPermissions.ErrorCode);
            var publicFolders = application.Settings.PublicFolders;
            Assert.AreEqual(1, publicFolders.Count);
            Assert.AreEqual(50, publicFolders[0].ID);
            Assert.AreEqual("Public", publicFolders[0].Name);
            var publicPermissions = publicFolders[0].Permissions;
            Assert.AreEqual(2, publicPermissions.Count);
            Assert.AreEqual(500, publicPermissions[0].ID);
            Assert.AreEqual(50, publicPermissions[0].ShareFolderID);
            Assert.AreEqual(ComAclPermissionType.Anyone, publicPermissions[0].PermissionType);
            Assert.AreEqual(0, publicPermissions[0].PermissionGroupID);
            Assert.AreEqual(0, publicPermissions[0].PermissionAccountID);
            Assert.AreEqual(3, publicPermissions[0].Value);
            Assert.IsTrue(publicPermissions[0].get_Permission(ComAclPermission.Lookup));
            Assert.IsTrue(publicPermissions[0].get_Permission(ComAclPermission.Read));
            Assert.IsFalse(publicPermissions[0].get_Permission(ComAclPermission.WriteSeen));
            Assert.AreEqual(501, publicPermissions.get_ItemByDBID(501).ID);
            Assert.AreEqual(501, publicPermissions.get_ItemByName("ACLPermission-501").ID);
            var outsidePermission = Assert.ThrowsExactly<COMException>(() => _ = publicPermissions.get_ItemByDBID(900));
            Assert.AreEqual(unchecked((int)0x8002000B), outsidePermission.ErrorCode);
            var pendingPermissionSave = Assert.ThrowsExactly<COMException>(publicPermissions[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingPermissionSave.ErrorCode);
            var routes = application.Settings.Routes;
            Assert.AreEqual(2, routes.Count);
            Assert.AreEqual("alpha.route.test", routes[0].DomainName);
            Assert.AreEqual("smtp.alpha.route.test", routes[0].TargetSMTPHost);
            Assert.AreEqual(2525, routes[0].TargetSMTPPort);
            Assert.AreEqual(4, routes[0].NumberOfTries);
            Assert.AreEqual(15, routes[0].MinutesBetweenTry);
            Assert.IsTrue(routes[0].AllAddresses);
            Assert.IsTrue(routes[0].RelayerRequiresAuth);
            Assert.AreEqual("relay-user", routes[0].RelayerAuthUsername);
            Assert.IsTrue(routes[0].TreatSecurityAsLocalDomain);
            Assert.IsTrue(routes[0].TreatRecipientAsLocalDomain);
            Assert.IsFalse(routes[0].TreatSenderAsLocalDomain);
            Assert.IsTrue(routes[0].UseSSL);
            Assert.AreEqual(ComConnectionSecurity.Tls, routes[0].ConnectionSecurity);
            Assert.AreEqual("Beta route", routes.get_ItemByName("BETA.ROUTE.TEST").Description);
            Assert.AreEqual(600, routes.get_ItemByDBID(600).ID);
            var alphaRouteAddresses = routes[0].Addresses;
            Assert.AreEqual(2, alphaRouteAddresses.Count);
            Assert.AreEqual("alpha-user@example.test", alphaRouteAddresses.get_ItemByDBID(1500).Address);
            Assert.AreEqual(500, alphaRouteAddresses.get_ItemByDBID(1501).RouteID);
            var outsideRouteAddress = Assert.ThrowsExactly<COMException>(
                () => _ = alphaRouteAddresses.get_ItemByDBID(1600));
            Assert.AreEqual(unchecked((int)0x8002000B), outsideRouteAddress.ErrorCode);
            var betaRouteAddresses = routes.get_ItemByDBID(600).Addresses;
            Assert.AreEqual(1, betaRouteAddresses.Count);
            Assert.AreEqual("beta-user@example.test", betaRouteAddresses.get_ItemByDBID(1600).Address);
            var pendingRouteAddressSave = Assert.ThrowsExactly<COMException>(
                alphaRouteAddresses.get_ItemByDBID(1500).Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingRouteAddressSave.ErrorCode);
            var incomingRelays = application.Settings.IncomingRelays;
            Assert.AreEqual(2, incomingRelays.Count);
            Assert.AreEqual("Alpha relay", incomingRelays[0].Name);
            Assert.AreEqual("127.0.0.1", incomingRelays[0].LowerIP);
            Assert.AreEqual("127.0.0.1", incomingRelays[0].UpperIP);
            Assert.AreEqual("Beta relay", incomingRelays.get_ItemByName("BETA RELAY").Name);
            Assert.AreEqual(800, incomingRelays.get_ItemByDBID(800).ID);
            var pendingRelaySave = Assert.ThrowsExactly<COMException>(incomingRelays[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingRelaySave.ErrorCode);
            var securityRanges = application.Settings.SecurityRanges;
            Assert.AreEqual(3, securityRanges.Count);
            Assert.AreEqual("My computer", securityRanges[0].Name);
            Assert.AreEqual("127.0.0.1", securityRanges[0].LowerIP);
            Assert.AreEqual("127.0.0.1", securityRanges[0].UpperIP);
            Assert.AreEqual(30, securityRanges[0].Priority);
            Assert.IsTrue(securityRanges[0].AllowSMTPConnections);
            Assert.IsTrue(securityRanges[0].AllowPOP3Connections);
            Assert.IsTrue(securityRanges[0].AllowIMAPConnections);
            Assert.IsTrue(securityRanges[0].RequireSSLTLSForAuth);
            Assert.IsFalse(securityRanges[0].Expires);
            Assert.AreEqual("0.0.0.0", securityRanges.get_ItemByName("internet").LowerIP);
            Assert.IsFalse(securityRanges.get_ItemByName("internet").AllowPOP3Connections);
            Assert.AreEqual("Auto-ban", securityRanges.get_ItemByDBID(300).Name);
            Assert.IsTrue(securityRanges.get_ItemByDBID(300).Expires);
            Assert.AreEqual(
                new DateTime(2026, 8, 1, 3, 4, 5),
                securityRanges.get_ItemByDBID(300).ExpiresTime);
            var pendingSecurityRangeSave = Assert.ThrowsExactly<COMException>(securityRanges[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingSecurityRangeSave.ErrorCode);
            var serverMessages = application.Settings.ServerMessages;
            Assert.AreEqual(2, serverMessages.Count);
            Assert.AreEqual("MESSAGE_UNDELIVERABLE", serverMessages[0].Name);
            Assert.AreEqual("Message undeliverable", serverMessages[0].Text);
            Assert.AreEqual("VIRUS_FOUND", serverMessages.get_ItemByDBID(951).Name);
            Assert.AreEqual(950, serverMessages.get_ItemByName("message_undeliverable").ID);
            var pendingServerMessageSave = Assert.ThrowsExactly<COMException>(serverMessages[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingServerMessageSave.ErrorCode);
            var tcpIpPorts = application.Settings.TCPIPPorts;
            Assert.AreEqual(2, tcpIpPorts.Count);
            Assert.AreEqual(25, tcpIpPorts[0].PortNumber);
            Assert.AreEqual(ComSessionType.Smtp, tcpIpPorts[0].Protocol);
            Assert.AreEqual("0.0.0.0", tcpIpPorts[0].Address);
            Assert.IsTrue(tcpIpPorts[0].UseSSL);
            Assert.AreEqual(123, tcpIpPorts[0].SSLCertificateID);
            Assert.AreEqual(ComConnectionSecurity.Tls, tcpIpPorts[0].ConnectionSecurity);
            Assert.AreEqual("127.0.0.1", tcpIpPorts.get_ItemByDBID(901).Address);
            Assert.IsFalse(tcpIpPorts.get_ItemByDBID(901).UseSSL);
            Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, tcpIpPorts.get_ItemByDBID(901).ConnectionSecurity);
            var pendingTcpIpPortSave = Assert.ThrowsExactly<COMException>(tcpIpPorts[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingTcpIpPortSave.ErrorCode);
            var sslCertificates = application.Settings.SSLCertificates;
            Assert.AreEqual(2, sslCertificates.Count);
            Assert.AreEqual("Alpha certificate", sslCertificates[0].Name);
            Assert.AreEqual(@"C:\certs\alpha.crt", sslCertificates[0].CertificateFile);
            Assert.AreEqual(@"C:\certs\alpha.key", sslCertificates[0].PrivateKeyFile);
            Assert.AreEqual("Beta certificate", sslCertificates.get_ItemByDBID(1002).Name);
            var pendingSslCertificateSave = Assert.ThrowsExactly<COMException>(sslCertificates[0].Save);
            Assert.AreEqual(unchecked((int)0x80004001), pendingSslCertificateSave.ErrorCode);
            var groups = application.Settings.Groups;
            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("Administrators", groups[0].Name);
            Assert.AreEqual(1100, groups[0].ID);
            Assert.AreEqual("Support", groups.get_ItemByDBID(1200).Name);
            Assert.AreEqual(1200, groups.get_ItemByName("SUPPORT").ID);
            var groupMembers = groups[0].Members;
            Assert.AreEqual(2, groupMembers.Count);
            Assert.AreEqual(1300, groupMembers[0].ID);
            Assert.AreEqual(1100, groupMembers[0].GroupID);
            Assert.AreEqual(10, groupMembers[0].AccountID);
            Assert.AreEqual(20, groupMembers.get_ItemByDBID(1400).AccountID);
            var pendingGroupMemberAccount = Assert.ThrowsExactly<COMException>(() => _ = groupMembers[0].Account);
            Assert.AreEqual(unchecked((int)0x80004001), pendingGroupMemberAccount.ErrorCode);
            Assert.AreEqual("user@example.test", accounts.get_ItemByAddress("USER@EXAMPLE.TEST").Address);
            Assert.IsFalse(accounts.get_ItemByDBID(20).Active);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_FetchAccountsStayScopedToSelectedAccountAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndAccountSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            AccountAdministrationRuntimeHost.Configure(new SqlServerAccountAdministrationStore(connectionFactory));
            FetchAccountAdministrationRuntimeHost.Configure(
                new SqlServerFetchAccountAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var accounts = application.Domains.get_ItemByName("example.test").Accounts;

            var adminFetchAccounts = accounts.get_ItemByDBID(10).FetchAccounts;
            Assert.AreEqual(1, adminFetchAccounts.Count);
            Assert.AreEqual(1000, adminFetchAccounts[0].ID);
            Assert.AreEqual("External POP3", adminFetchAccounts.get_ItemByDBID(1000).Name);
            Assert.AreEqual("pop3.example.test", adminFetchAccounts[0].ServerAddress);
            Assert.AreEqual(995, adminFetchAccounts[0].Port);
            Assert.AreEqual("external-user", adminFetchAccounts[0].Username);
            Assert.AreEqual(15, adminFetchAccounts[0].MinutesBetweenFetch);
            Assert.AreEqual(14, adminFetchAccounts[0].DaysToKeepMessages);
            Assert.IsTrue(adminFetchAccounts[0].Enabled);
            Assert.IsTrue(adminFetchAccounts[0].ProcessMIMERecipients);
            Assert.IsTrue(adminFetchAccounts[0].ProcessMIMEDate);
            Assert.AreEqual(ComConnectionSecurity.Tls, adminFetchAccounts[0].ConnectionSecurity);
            Assert.IsTrue(adminFetchAccounts[0].UseSSL);
            Assert.IsTrue(adminFetchAccounts[0].UseAntiSpam);
            Assert.IsTrue(adminFetchAccounts[0].UseAntiVirus);
            Assert.IsTrue(adminFetchAccounts[0].EnableRouteRecipients);
            Assert.AreEqual("To,CC,X-RCPT-TO", adminFetchAccounts[0].MIMERecipientHeaders);
            Assert.AreEqual("2026-07-01 02:03:04", adminFetchAccounts[0].NextDownloadTime);
            Assert.IsTrue(adminFetchAccounts[0].IsLocked);

            var outsideAccountLookup = Assert.ThrowsExactly<COMException>(
                () => _ = adminFetchAccounts.get_ItemByDBID(2000));
            var pendingPasswordRead = Assert.ThrowsExactly<COMException>(() => _ = adminFetchAccounts[0].Password);
            Assert.AreEqual(unchecked((int)0x8002000B), outsideAccountLookup.ErrorCode);
            Assert.AreEqual(unchecked((int)0x80004001), pendingPasswordRead.ErrorCode);

            var userFetchAccounts = accounts.get_ItemByDBID(20).FetchAccounts;
            Assert.AreEqual(1, userFetchAccounts.Count);
            Assert.AreEqual(2000, userFetchAccounts[0].ID);
            Assert.AreEqual(20, userFetchAccounts[0].AccountID);
            Assert.AreEqual("User POP3", userFetchAccounts[0].Name);
            Assert.AreEqual("pop3-user.example.test", userFetchAccounts[0].ServerAddress);
            Assert.AreEqual(110, userFetchAccounts[0].Port);
            Assert.AreEqual("user-external", userFetchAccounts[0].Username);
            Assert.AreEqual(30, userFetchAccounts[0].MinutesBetweenFetch);
            Assert.AreEqual(7, userFetchAccounts[0].DaysToKeepMessages);
            Assert.IsTrue(userFetchAccounts[0].Enabled);
            Assert.IsFalse(userFetchAccounts[0].ProcessMIMERecipients);
            Assert.IsFalse(userFetchAccounts[0].ProcessMIMEDate);
            Assert.AreEqual(ComConnectionSecurity.None, userFetchAccounts[0].ConnectionSecurity);
            Assert.IsFalse(userFetchAccounts[0].UseSSL);
            Assert.IsFalse(userFetchAccounts[0].UseAntiSpam);
            Assert.IsFalse(userFetchAccounts[0].UseAntiVirus);
            Assert.IsFalse(userFetchAccounts[0].EnableRouteRecipients);
            Assert.AreEqual("To,CC", userFetchAccounts[0].MIMERecipientHeaders);
            Assert.AreEqual("2026-07-02 02:03:04", userFetchAccounts[0].NextDownloadTime);
            Assert.IsFalse(userFetchAccounts[0].IsLocked);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesAliasLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndAliasSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            AliasAdministrationRuntimeHost.Configure(new SqlServerAliasAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domain = application.Domains.get_ItemByName("example.test");
            var aliases = domain.Aliases;

            Assert.AreEqual(2, aliases.Count);
            Assert.AreEqual("abuse@example.test", aliases[0].Name);
            Assert.AreEqual("admin@example.test", aliases[0].Value);
            Assert.AreEqual(10, aliases[0].DomainID);
            Assert.AreEqual("sales@example.test", aliases.get_ItemByName("SALES@EXAMPLE.TEST").Name);
            Assert.IsFalse(aliases.get_ItemByDBID(20).Active);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesDistributionListLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndDistributionListSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            DistributionListAdministrationRuntimeHost.Configure(
                new SqlServerDistributionListAdministrationStore(connectionFactory));
            DistributionListRecipientAdministrationRuntimeHost.Configure(
                new SqlServerDistributionListRecipientAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domain = application.Domains.get_ItemByName("example.test");
            var lists = domain.DistributionLists;

            Assert.AreEqual(2, lists.Count);
            Assert.AreEqual("announce@example.test", lists[0].Address);
            Assert.AreEqual(10, lists[0].ID);
            Assert.IsTrue(lists[0].Active);
            Assert.IsFalse(lists[0].RequireSMTPAuth);
            Assert.AreEqual(ComDistributionListMode.Public, lists[0].Mode);
            Assert.AreEqual("members@example.test", lists.get_ItemByAddress("MEMBERS@EXAMPLE.TEST").Address);
            Assert.AreEqual("owner@example.test", lists.get_ItemByDBID(20).RequireSenderAddress);
            Assert.IsFalse(lists.get_ItemByDBID(20).Active);
            Assert.AreEqual(ComDistributionListMode.Membership, lists.get_ItemByDBID(20).Mode);
            var recipients = lists.get_ItemByDBID(20).Recipients;
            Assert.AreEqual(2, recipients.Count);
            Assert.AreEqual("alpha@example.test", recipients[0].RecipientAddress);
            Assert.AreEqual("zeta@example.test", recipients.get_ItemByDBID(200).RecipientAddress);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesDomainAliasLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndDomainAliasSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            DomainAliasAdministrationRuntimeHost.Configure(new SqlServerDomainAliasAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domain = application.Domains.get_ItemByName("example.test");
            var aliases = domain.DomainAliases;

            Assert.AreEqual(2, aliases.Count);
            Assert.AreEqual("alias-one.test", aliases[0].AliasName);
            Assert.AreEqual(10, aliases[0].DomainID);
            Assert.AreEqual("alias-two.test", aliases.get_ItemByDBID(20).AliasName);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}];", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messagetype int NOT NULL
);

CREATE TABLE dbo.hm_settings
(
    settingname nvarchar(255) NOT NULL PRIMARY KEY,
    settingstring nvarchar(max) NOT NULL,
    settinginteger int NOT NULL
);

CREATE TABLE dbo.hm_message_search_documents
(
    messageid bigint NOT NULL PRIMARY KEY
);

CREATE TABLE dbo.hm_message_search_queue
(
    messageid bigint NOT NULL PRIMARY KEY,
    queuedutc datetime2(3) NOT NULL,
    attempts int NOT NULL,
    lastattemptutc datetime2(3) NULL,
    nextattemptutc datetime2(3) NULL,
    searchleaseowner nvarchar(128) NULL,
    searchleaseexpiresutc datetime2(3) NULL,
    lasterror nvarchar(1024) NULL
);

INSERT INTO dbo.hm_messages (messageid, messagetype)
VALUES (1, 2), (2, 2), (3, 3);

INSERT INTO dbo.hm_message_search_documents (messageid)
VALUES (1);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSettingsSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_settings
(
    settingname nvarchar(30) NOT NULL PRIMARY KEY,
    settingstring nvarchar(4000) NOT NULL,
    settinginteger int NOT NULL
);

CREATE TABLE dbo.hm_blocked_attachments
(
    baid int NOT NULL PRIMARY KEY,
    bawildcard nvarchar(255) NOT NULL,
    badescription nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_dnsbl
(
    sblid int NOT NULL PRIMARY KEY,
    sblactive int NOT NULL,
    sbldnshost nvarchar(255) NOT NULL,
    sblresult nvarchar(255) NOT NULL,
    sblrejectmessage nvarchar(255) NOT NULL,
    sblscore int NOT NULL
);

CREATE TABLE dbo.hm_surblservers
(
    surblid int NOT NULL PRIMARY KEY,
    surblactive tinyint NOT NULL,
    surblhost nvarchar(255) NOT NULL,
    surblrejectmessage nvarchar(255) NOT NULL,
    surblscore int NOT NULL
);

CREATE TABLE dbo.hm_greylisting_whiteaddresses
(
    whiteid bigint NOT NULL PRIMARY KEY,
    whiteipaddress nvarchar(255) NOT NULL,
    whiteipdescription nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_whitelist
(
    whiteid bigint NOT NULL PRIMARY KEY,
    whiteloweripaddress1 bigint NOT NULL,
    whiteloweripaddress2 bigint NULL,
    whiteupperipaddress1 bigint NOT NULL,
    whiteupperipaddress2 bigint NULL,
    whiteemailaddress nvarchar(255) NOT NULL,
    whitedescription nvarchar(255) NOT NULL
);

INSERT INTO dbo.hm_settings (settingname, settingstring, settinginteger)
VALUES
    (N'hostname', N'mail.example.test', 0),
    (N'welcomesmtp', N'SMTP ready', 0),
    (N'welcomepop3', N'POP3 ready', 0),
    (N'welcomeimap', N'IMAP ready', 0),
    (N'maxsmtpconnections', N'', 100),
    (N'maxpop3connections', N'', 50),
    (N'maximapconnections', N'', 75),
    (N'maxdelivertythreads', N'', 10),
    (N'protocolsmtp', N'', 1),
    (N'protocolpop3', N'', 0),
    (N'protocolimap', N'', 1),
    (N'smtpnoofretries', N'', 4),
    (N'smtpminutesbetweenretries', N'', 60),
    (N'smtpnooftries', N'', 999),
    (N'maxmessagesize', N'', 20480),
    (N'maxsmtprecipientsinbatch', N'', 100),
    (N'disconnectinvalidclients', N'', 1),
    (N'maximumincorrectcommands', N'', 12),
    (N'enableimapsort', N'', 1),
    (N'enableimapquota', N'', 0),
    (N'enableimapidle', N'', 1),
    (N'enableimapacl', N'', 0),
    (N'EnableImapSASLPlain', N'', 1),
    (N'EnableImapSASLInitialResponse', N'', 0),
    (N'imappublicfoldername', N'#Shared', 0),
    (N'IMAPHierarchyDelimiter', N'/', 0),
    (N'authallowplaintext', N'', 1),
    (N'allowmailfromnull', N'', 0),
    (N'smtpallowincorrectlineendings', N'', 1),
    (N'adddeliveredtoheader', N'', 0),
    (N'mirroremailaddress', N'archive@example.test', 0),
    (N'defaultdomain', N'example.test', 0),
    (N'smtpdeliverybindtoip', N'192.0.2.25', 0),
    (N'rulelooplimit', N'', 9),
    (N'workerthreadpriority', N'', -1),
    (N'tcpipthreads', N'', 16),
    (N'MaxNumberOfMXHosts', N'', 22),
    (N'VerifyRemoteSslCertificate', N'', 1),
    (N'SslCipherList', N'TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256', 0),
    (N'IPv6Preferred', N'', 1),
    (N'AutoBanOnLogonFailureEnabled', N'', 1),
    (N'MaxInvalidLogonAttempts', N'', 3),
    (N'LogonAttemptsWithinMinutes', N'', 30),
    (N'AutoBanMinutes', N'', 60),
    (N'smtprelayer', N'relay.example.test', 0),
    (N'usesmtprelayerauthentication', N'', 1),
    (N'smtprelayerusername', N'relay-user', 0),
    (N'smtprelayerport', N'', 587),
    (N'smtprelayerconnectionsecurity', N'', 3),
    (N'SmtpDeliveryConnectionSecurity', N'', 2),
    (N'SslVersions', N'', 26),
    (N'TlsOptions', N'', 2),
    (N'ImapMasterUser', N'master-user', 0),
    (N'MaxNumberOfAsynchronousTasks', N'', 15),
    (N'logging', N'', 379),
    (N'logdevice', N'', 2),
    (N'logformat', N'', 1),
    (N'awstatsenabled', N'', 1),
    (N'usescriptserver', N'', 1),
    (N'scriptlanguage', N'JScript', 0),
    (N'backupdestination', N'D:\hMailServer Backup', 0),
    (N'backupoptions', N'', 13),
    (N'avclamwinenable', N'', 1),
    (N'avclamwinexec', N'C:\ClamWin\bin\clamscan.exe', 0),
    (N'avclamwindb', N'C:\ClamWin\db', 0),
    (N'avaction', N'', 1),
    (N'avnotifyreceiver', N'', 1),
    (N'avnotifysender', N'', 0),
    (N'usecustomvirusscanner', N'', 1),
    (N'customvirusscannerexecutable', N'C:\Tools\virus-scan.cmd', 0),
    (N'customviursscannerreturnvalue', N'', 7),
    (N'avmaxmsgsize', N'', 4096),
    (N'enableattachmentblocking', N'', 1),
    (N'ClamAVEnabled', N'', 1),
    (N'ClamAVHost', N'127.0.0.1', 0),
    (N'ClamAVPort', N'', 3310),
    (N'usegreylisting', N'', 1),
    (N'greylistinginitialdelay', N'', 30),
    (N'greylistinginitialdelete', N'', 48),
    (N'greylistingfinaldelete', N'', 864),
    (N'ascheckhostinhelo', N'', 1),
    (N'ascheckhostinheloscore', N'', 2),
    (N'ascheckptr', N'', 1),
    (N'ascheckptrscore', N'', 4),
    (N'antispamaddheaderspam', N'', 1),
    (N'antispamaddheaderreason', N'', 0),
    (N'antispamprependsubject', N'', 1),
    (N'antispamprependsubjecttext', N'[SPAM]', 0),
    (N'spammarkthreshold', N'', 5),
    (N'spamdeletethreshold', N'', 20),
    (N'usespf', N'', 1),
    (N'usespfscore', N'', 3),
    (N'usemxchecks', N'', 1),
    (N'usemxchecksscore', N'', 6),
    (N'spamassassinenabled', N'', 1),
    (N'spamassassinscore', N'', 7),
    (N'spamassassinmergescore', N'', 0),
    (N'spamassassinhost', N'spamd.example.test', 0),
    (N'spamassassinport', N'', 783),
    (N'antispammaxsize', N'', 1024),
    (N'ASDKIMVerificationEnabled', N'', 1),
    (N'ASDKIMVerificationFailureScore', N'', 8),
    (N'BypassGreylistingOnSPFSuccess', N'', 1),
    (N'BypassGreylistingOnMailFromMX', N'', 0),
    (N'usecache', N'', 1),
    (N'domaincachettl', N'', 61),
    (N'accountcachettl', N'', 62),
    (N'aliascachettl', N'', 63),
    (N'distributionlistcachettl', N'', 64),
    (N'smtprelayerpassword', N'must-not-be-read', 0);

INSERT INTO dbo.hm_blocked_attachments (baid, bawildcard, badescription)
VALUES
    (20, N'*.exe', N'Executable file'),
    (10, N'*.bat', N'Batch file');

INSERT INTO dbo.hm_dnsbl (sblid, sblactive, sbldnshost, sblresult, sblrejectmessage, sblscore)
VALUES
    (20, 0, N'bl.spamcop.net', N'127.0.0.2', N'Rejected by SpamCop.', 3),
    (10, 1, N'zen.spamhaus.org', N'127.0.0.2-8|127.0.0.10-11', N'Rejected by Spamhaus.', 4);

INSERT INTO dbo.hm_surblservers (surblid, surblactive, surblhost, surblrejectmessage, surblscore)
VALUES
    (20, 0, N'example.surbl.test', N'Rejected by test SURBL.', 2),
    (10, 1, N'multi.surbl.org', N'Rejected by SURBL.', 4);

INSERT INTO dbo.hm_greylisting_whiteaddresses (whiteid, whiteipaddress, whiteipdescription)
VALUES
    (20, N'203.0.113.5', N'Single address'),
    (10, N'192.0.2.%', N'Test network');

INSERT INTO dbo.hm_whitelist
    (whiteid, whiteloweripaddress1, whiteloweripaddress2,
     whiteupperipaddress1, whiteupperipaddress2, whiteemailaddress, whitedescription)
VALUES
    (20, 3405803781, NULL, 3405803781, NULL, N'sender@example.test', N'Single address'),
    (10, 3221225985, NULL, 3221226239, NULL, N'*@example.test', N'Test network');
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDeliveryQueueAdministrationSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messagefilename nvarchar(255) NOT NULL,
    messagetype int NOT NULL,
    messagenexttrytime datetime2 NOT NULL,
    messagecurnooftries int NOT NULL,
    messagelocked int NOT NULL,
    messageleaseowner nvarchar(128) NULL,
    messageleaseexpiresutc datetime2 NULL
);

CREATE TABLE dbo.hm_messagerecipients
(
    recipientid bigint NOT NULL PRIMARY KEY,
    recipientmessageid bigint NOT NULL
);

INSERT INTO dbo.hm_messages
(
    messageid,
    messagefilename,
    messagetype,
    messagenexttrytime,
    messagecurnooftries,
    messagelocked,
    messageleaseowner,
    messageleaseexpiresutc
)
VALUES
    (10, N'active.eml', 3, '2099-01-01T00:00:00', 7, 1, N'worker-a', '2099-01-01T00:00:00'),
    (20, N'delivered.eml', 2, '2099-01-01T00:00:00', 9, 0, NULL, NULL),
    (30, N'ready.eml', 1, '1901-01-01T00:00:00', 0, 0, NULL, NULL),
    (40, N'expired.eml', 1, '1901-01-01T00:00:00', 1, 1, N'worker-expired', '2000-01-01T00:00:00'),
    (50, N'clear-a.eml', 1, '1901-01-01T00:00:00', 0, 0, NULL, NULL),
    (60, N'clear-b.eml', 3, '1901-01-01T00:00:00', 2, 0, NULL, NULL);

INSERT INTO dbo.hm_messagerecipients
    (recipientid, recipientmessageid)
VALUES
    (1, 10),
    (2, 20),
    (3, 30),
    (4, 40),
    (5, 50),
    (6, 60);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL,
    domainpostmaster nvarchar(80) NOT NULL,
    domainmaxmessagesize int NOT NULL,
    domainuseplusaddressing tinyint NOT NULL,
    domainplusaddressingchar nvarchar(1) NOT NULL,
    domainaddomain nvarchar(255) NOT NULL,
    domainmaxsize int NOT NULL,
    domainmaxnoofaccounts int NOT NULL,
    domainmaxnoofaliases int NOT NULL,
    domainmaxnoofdistributionlists int NOT NULL,
    domainlimitationsenabled int NOT NULL,
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

CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountmaxsize int NOT NULL
);

CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
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

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainaddomain, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (20, N'beta.example', 0, N'postmaster@beta.example', 512, 0, N'+', N'', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (10, N'alpha.example', 1, N'postmaster@alpha.example', 1024, 1, N'+', N'corp.alpha.example', 4096, 200, 30, 12, 5, 512, 1, 3, N'Alpha plain signature', N'<p>Alpha HTML signature</p>', 1, 0, 55, N'alpha-selector', N'C:\keys\alpha.pem');

INSERT INTO dbo.hm_accounts (accountid, accountdomainid, accountmaxsize)
VALUES
    (100, 10, 1024),
    (101, 10, 2048),
    (200, 20, 128);

INSERT INTO dbo.hm_messages (messageid, messageaccountid, messagesize)
VALUES
    (1000, 10, 2621440),
    (1001, 100, 7340032),
    (2000, 20, 1048576);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainAndAccountSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL,
    domainpostmaster nvarchar(80) NOT NULL,
    domainmaxmessagesize int NOT NULL,
    domainuseplusaddressing tinyint NOT NULL,
    domainplusaddressingchar nvarchar(1) NOT NULL,
    domainaddomain nvarchar(255) NOT NULL,
    domainmaxsize int NOT NULL,
    domainmaxnoofaccounts int NOT NULL,
    domainmaxnoofaliases int NOT NULL,
    domainmaxnoofdistributionlists int NOT NULL,
    domainlimitationsenabled int NOT NULL,
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

CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountaddress nvarchar(255) NOT NULL,
    accountactive tinyint NOT NULL,
    accountadminlevel tinyint NOT NULL,
    accountisad int NOT NULL,
    accountaddomain nvarchar(255) NOT NULL,
    accountadusername nvarchar(255) NOT NULL,
    accountmaxsize int NOT NULL,
    accountpersonfirstname nvarchar(60) NOT NULL,
    accountpersonlastname nvarchar(60) NOT NULL,
    accountvacationmessageon tinyint NOT NULL,
    accountvacationmessage nvarchar(1000) NOT NULL,
    accountvacationsubject nvarchar(200) NOT NULL,
    accountvacationexpires tinyint NOT NULL,
    accountvacationexpiredate datetime NOT NULL,
    accountvacationabortspamflagged tinyint NOT NULL,
    accountforwardenabled tinyint NOT NULL,
    accountforwardaddress nvarchar(255) NOT NULL,
    accountforwardkeeporiginal tinyint NOT NULL,
    accountforwardabortspamflagged tinyint NOT NULL,
    accountenablesignature tinyint NOT NULL,
    accountsignatureplaintext nvarchar(max) NOT NULL,
    accountsignaturehtml nvarchar(max) NOT NULL,
    accountlastlogontime datetime NOT NULL
);

CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messageaccountid int NOT NULL,
    messagesize bigint NOT NULL
);

CREATE TABLE dbo.hm_fetchaccounts
(
    faid int NOT NULL PRIMARY KEY,
    faactive tinyint NOT NULL,
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
    falocked tinyint NOT NULL,
    faprocessmimerecipients tinyint NOT NULL,
    faprocessmimedate tinyint NOT NULL,
    faconnectionsecurity tinyint NOT NULL,
    fauseantispam tinyint NOT NULL,
    fauseantivirus tinyint NOT NULL,
    faenablerouterecipients tinyint NOT NULL,
    famimerecipientheaders nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_rules
(
    ruleid int NOT NULL PRIMARY KEY,
    ruleaccountid int NOT NULL,
    rulename nvarchar(100) NOT NULL,
    ruleactive tinyint NOT NULL,
    ruleuseand tinyint NOT NULL,
    rulesortorder int NOT NULL
);

CREATE TABLE dbo.hm_rule_criterias
(
    criteriaid int NOT NULL PRIMARY KEY,
    criteriaruleid int NOT NULL,
    criteriausepredefined tinyint NOT NULL,
    criteriapredefinedfield tinyint NOT NULL,
    criteriaheadername nvarchar(255) NOT NULL,
    criteriamatchtype tinyint NOT NULL,
    criteriamatchvalue nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_rule_actions
(
    actionid int NOT NULL PRIMARY KEY,
    actionruleid int NOT NULL,
    actiontype tinyint NOT NULL,
    actionimapfolder nvarchar(255) NOT NULL,
    actionsubject nvarchar(255) NOT NULL,
    actionfromname nvarchar(255) NOT NULL,
    actionfromaddress nvarchar(255) NOT NULL,
    actionto nvarchar(255) NOT NULL,
    actionbody nvarchar(max) NOT NULL,
    actionfilename nvarchar(255) NOT NULL,
    actionsortorder int NOT NULL,
    actionscriptfunction nvarchar(255) NOT NULL,
    actionheader nvarchar(80) NOT NULL,
    actionvalue nvarchar(255) NOT NULL,
    actionrouteid int NOT NULL,
    actionabortspamflagged tinyint NOT NULL
);

CREATE TABLE dbo.hm_imapfolders
(
    folderid int NOT NULL PRIMARY KEY,
    folderaccountid int NOT NULL,
    folderparentid int NOT NULL,
    foldername nvarchar(255) NOT NULL,
    folderissubscribed tinyint NOT NULL,
    foldercreationtime datetime NOT NULL,
    foldercurrentuid bigint NOT NULL
);

CREATE TABLE dbo.hm_acl
(
    aclid bigint NOT NULL PRIMARY KEY,
    aclsharefolderid bigint NOT NULL,
    aclpermissiontype tinyint NOT NULL,
    aclpermissiongroupid bigint NOT NULL,
    aclpermissionaccountid bigint NOT NULL,
    aclvalue bigint NOT NULL
);

CREATE TABLE dbo.hm_routes
(
    routeid int NOT NULL PRIMARY KEY,
    routedomainname nvarchar(255) NOT NULL,
    routedescription nvarchar(255) NOT NULL,
    routetargetsmthost nvarchar(255) NOT NULL,
    routetargetsmtport int NOT NULL,
    routenooftries int NOT NULL,
    routeminutesbetweentry int NOT NULL,
    routealladdresses tinyint NOT NULL,
    routeuseauthentication tinyint NOT NULL,
    routeauthenticationusername nvarchar(255) NOT NULL,
    routeauthenticationpassword nvarchar(255) NOT NULL,
    routetreatsecurityaslocal tinyint NOT NULL,
    routeconnectionsecurity tinyint NOT NULL,
    routetreatsenderaslocaldomain tinyint NOT NULL
);

CREATE TABLE dbo.hm_routeaddresses
(
    routeaddressid int NOT NULL PRIMARY KEY,
    routeaddressrouteid int NOT NULL,
    routeaddressaddress nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_incoming_relays
(
    relayid int NOT NULL PRIMARY KEY,
    relayname nvarchar(100) NOT NULL,
    relaylowerip1 bigint NOT NULL,
    relaylowerip2 bigint NULL,
    relayupperip1 bigint NOT NULL,
    relayupperip2 bigint NULL
);

CREATE TABLE dbo.hm_securityranges
(
    rangeid int NOT NULL PRIMARY KEY,
    rangename nvarchar(100) NOT NULL,
    rangepriorityid int NOT NULL,
    rangelowerip1 bigint NOT NULL,
    rangelowerip2 bigint NULL,
    rangeupperip1 bigint NOT NULL,
    rangeupperip2 bigint NULL,
    rangeoptions int NOT NULL,
    rangeexpires tinyint NOT NULL,
    rangeexpirestime datetime NOT NULL
);

CREATE TABLE dbo.hm_tcpipports
(
    portid int NOT NULL PRIMARY KEY,
    portprotocol int NOT NULL,
    portnumber int NOT NULL,
    portaddress1 bigint NOT NULL,
    portaddress2 bigint NULL,
    portconnectionsecurity tinyint NOT NULL,
    portsslcertificateid bigint NOT NULL
);

CREATE TABLE dbo.hm_servermessages
(
    smid int NOT NULL PRIMARY KEY,
    smname nvarchar(255) NOT NULL,
    smtext nvarchar(max) NOT NULL
);

CREATE TABLE dbo.hm_sslcertificates
(
    sslcertificateid bigint NOT NULL PRIMARY KEY,
    sslcertificatename nvarchar(255) NOT NULL,
    sslcertificatefile nvarchar(255) NOT NULL,
    sslprivatekeyfile nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_groups
(
    groupid bigint NOT NULL PRIMARY KEY,
    groupname nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_group_members
(
    memberid bigint NOT NULL PRIMARY KEY,
    membergroupid bigint NOT NULL,
    memberaccountid bigint NOT NULL
);

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainaddomain, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', N'', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 54, N'example-selector', N'C:\keys\example.pem'),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', N'', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

INSERT INTO dbo.hm_accounts
    (accountid, accountdomainid, accountaddress, accountactive, accountadminlevel,
     accountisad, accountaddomain, accountadusername,
     accountmaxsize, accountpersonfirstname, accountpersonlastname,
     accountvacationmessageon, accountvacationmessage, accountvacationsubject,
     accountvacationexpires, accountvacationexpiredate, accountvacationabortspamflagged,
     accountforwardenabled, accountforwardaddress, accountforwardkeeporiginal,
     accountforwardabortspamflagged, accountenablesignature, accountsignatureplaintext,
     accountsignaturehtml, accountlastlogontime)
VALUES
    (20, 10, N'user@example.test', 0, 0, 0, N'', N'', 1024, N'Grace', N'Hopper',
     0, N'', N'', 0, CONVERT(datetime, '2026-01-01T00:00:00', 126), 0,
     0, N'', 0, 0, 0, N'', N'', CONVERT(datetime, '2026-02-03T04:05:06', 126)),
    (10, 10, N'admin@example.test', 1, 2, 1, N'corp.example.test', N'ada.lovelace', 2, N'Ada', N'Lovelace',
     1, N'Away until Monday', N'Auto reply', 1, CONVERT(datetime, '2026-12-31T00:00:00', 126), 1,
     1, N'archive@example.test', 1, 1, 1, N'Regards,' + CHAR(13) + CHAR(10) + N'Ada', N'<p>Regards,<br>Ada</p>', CONVERT(datetime, '2026-03-04T05:06:07', 126)),
    (30, 30, N'outside@other.test', 1, 0, 0, N'', N'', 512, N'Outside', N'Example',
     0, N'', N'', 0, CONVERT(datetime, '2026-01-01T00:00:00', 126), 0,
     0, N'', 0, 0, 0, N'', N'', CONVERT(datetime, '2026-04-05T06:07:08', 126));

INSERT INTO dbo.hm_messages
    (messageid, messageaccountid, messagefolderid, messagefilename, messagetype,
     messagefrom, messagesize, messagecurnooftries, messagenexttrytime, messageflags,
     messagecreatetime, messagelocked, messageuid)
VALUES
    (3000, 10, 100, N'admin-inbox.eml', 2, N'sender@example.test', 2621440, 2,
     CONVERT(datetime, '1901-01-01T00:00:00', 126), 33, CONVERT(datetime, '2026-07-01T01:02:03', 126), 0, 41),
    (3001, 20, 400, N'user-inbox.eml', 2, N'user-sender@example.test', 131072, 0,
     CONVERT(datetime, '1901-01-01T00:00:00', 126), 2, CONVERT(datetime, '2026-07-02T01:02:03', 126), 0, 42),
    (3002, 30, 500, N'outside-inbox.eml', 2, N'outside-sender@example.test', 1048576, 0,
     CONVERT(datetime, '1901-01-01T00:00:00', 126), 0, CONVERT(datetime, '2026-07-03T01:02:03', 126), 0, 43);

INSERT INTO dbo.hm_fetchaccounts
    (faid, faactive, faaccountid, faaccountname, faserveraddress, faserverport,
     faservertype, fausername, fapassword, faminutes, fanexttry, fadaystokeep,
     falocked, faprocessmimerecipients, faprocessmimedate, faconnectionsecurity,
     fauseantispam, fauseantivirus, faenablerouterecipients, famimerecipientheaders)
VALUES
    (1000, 1, 10, N'External POP3', N'pop3.example.test', 995,
     0, N'external-user', N'not-exposed', 15, CONVERT(datetime, '2026-07-01T02:03:04', 126), 14,
     1, 1, 1, 1, 1, 1, 1, N'To,CC,X-RCPT-TO'),
    (2000, 1, 20, N'User POP3', N'pop3-user.example.test', 110,
     0, N'user-external', N'not-exposed', 30, CONVERT(datetime, '2026-07-02T02:03:04', 126), 7,
     0, 0, 0, 0, 0, 0, 0, N'To,CC');

INSERT INTO dbo.hm_rules
    (ruleid, ruleaccountid, rulename, ruleactive, ruleuseand, rulesortorder)
VALUES
    (150, 0, N'Global second', 0, 0, 2),
    (100, 0, N'Global first', 1, 1, 1),
    (300, 10, N'Second rule', 0, 0, 2),
    (200, 10, N'First rule', 1, 1, 1),
    (400, 20, N'User rule', 1, 1, 1);

INSERT INTO dbo.hm_rule_criterias
    (criteriaid, criteriaruleid, criteriausepredefined, criteriapredefinedfield,
     criteriaheadername, criteriamatchtype, criteriamatchvalue)
VALUES
    (1000, 100, 1, 1, N'', 1, N'sender@example.test'),
    (2000, 200, 1, 4, N'', 2, N'invoice'),
    (2001, 200, 0, 0, N'X-Priority', 1, N'high'),
    (3000, 300, 1, 2, N'', 1, N'user@example.test'),
    (4000, 400, 1, 3, N'', 2, N'support@example.test');

INSERT INTO dbo.hm_rule_actions
    (actionid, actionruleid, actiontype, actionimapfolder, actionsubject,
     actionfromname, actionfromaddress, actionto, actionbody, actionfilename,
     actionsortorder, actionscriptfunction, actionheader, actionvalue,
     actionrouteid, actionabortspamflagged)
VALUES
    (10000, 100, 7, N'', N'', N'', N'', N'', N'', N'', 1, N'', N'X-Global', N'yes', 0, 0),
    (20001, 200, 8, N'', N'', N'', N'', N'', N'', N'', 2, N'', N'', N'', 500, 0),
    (20000, 200, 3, N'Processed', N'Invoice received', N'Billing', N'billing@example.test',
     N'sender@example.test', N'Thank you', N'reply.eml', 1, N'HandleInvoice',
     N'X-Processed', N'yes', 500, 1),
    (30000, 300, 1, N'', N'', N'', N'', N'', N'', N'', 1, N'', N'', N'', 0, 0),
    (40000, 400, 2, N'', N'', N'', N'', N'', N'', N'', 1, N'', N'', N'', 0, 0);

INSERT INTO dbo.hm_imapfolders
    (folderid, folderaccountid, folderparentid, foldername, folderissubscribed,
     foldercreationtime, foldercurrentuid)
VALUES
    (50, 0, -1, N'Public', 1, CONVERT(datetime, '2026-06-27T00:02:03', 126), 5),
    (100, 10, -1, N'Inbox', 1, CONVERT(datetime, '2026-06-27T01:02:03', 126), 42),
    (200, 10, 100, N'Child', 1, CONVERT(datetime, '2026-06-27T01:03:03', 126), 3),
    (300, 10, -1, N'TE&AOUA5AD2-ST', 0, CONVERT(datetime, '2026-06-26T04:05:06', 126), 7),
    (400, 20, -1, N'User Inbox', 1, CONVERT(datetime, '2026-06-27T01:02:03', 126), 1);

INSERT INTO dbo.hm_acl
    (aclid, aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid, aclvalue)
VALUES
    (500, 50, 2, 0, 0, 3),
    (501, 50, 0, 0, 10, 1025),
    (900, 100, 0, 0, 10, 3);

INSERT INTO dbo.hm_routes
    (routeid, routedomainname, routedescription, routetargetsmthost, routetargetsmtport,
     routenooftries, routeminutesbetweentry, routealladdresses, routeuseauthentication,
     routeauthenticationusername, routeauthenticationpassword, routetreatsecurityaslocal,
     routeconnectionsecurity, routetreatsenderaslocaldomain)
VALUES
    (600, N'beta.route.test', N'Beta route', N'smtp.beta.route.test', 587,
     3, 10, 0, 0, N'', N'not-exposed', 0, 3, 1),
    (500, N'alpha.route.test', N'Alpha route', N'smtp.alpha.route.test', 2525,
     4, 15, 1, 1, N'relay-user', N'not-exposed', 1, 1, 0);

INSERT INTO dbo.hm_routeaddresses
    (routeaddressid, routeaddressrouteid, routeaddressaddress)
VALUES
    (1500, 500, N'alpha-user@example.test'),
    (1501, 500, N'*@alpha.route.test'),
    (1600, 600, N'beta-user@example.test');

INSERT INTO dbo.hm_incoming_relays
    (relayid, relayname, relaylowerip1, relaylowerip2, relayupperip1, relayupperip2)
VALUES
    (800, N'Beta relay', 167772160, NULL, 167772415, NULL),
    (700, N'Alpha relay', 2130706433, NULL, 2130706433, NULL);

INSERT INTO dbo.hm_securityranges
    (rangeid, rangename, rangepriorityid, rangelowerip1, rangelowerip2,
     rangeupperip1, rangeupperip2, rangeoptions, rangeexpires, rangeexpirestime)
VALUES
    (200, N'Internet', 10, 0, NULL, 4294967295, NULL, 1, 0, CONVERT(datetime, '2001-01-01T00:00:00', 126)),
    (100, N'My computer', 30, 2130706433, NULL, 2130706433, NULL, 260043, 0, CONVERT(datetime, '2001-01-01T00:00:00', 126)),
    (300, N'Auto-ban', 100, 167772161, NULL, 167772161, NULL, 0, 1, CONVERT(datetime, '2026-08-01T03:04:05', 126));

INSERT INTO dbo.hm_tcpipports
    (portid, portprotocol, portnumber, portaddress1, portaddress2,
     portconnectionsecurity, portsslcertificateid)
VALUES
    (901, 5, 143, 2130706433, NULL, 3, 0),
    (900, 1, 25, 0, NULL, 1, 123);

INSERT INTO dbo.hm_servermessages (smid, smname, smtext)
VALUES
    (951, N'VIRUS_FOUND', N'Virus found'),
    (950, N'MESSAGE_UNDELIVERABLE', N'Message undeliverable');

INSERT INTO dbo.hm_sslcertificates
    (sslcertificateid, sslcertificatename, sslcertificatefile, sslprivatekeyfile)
VALUES
    (1002, N'Beta certificate', N'C:\certs\beta.crt', N'C:\certs\beta.key'),
    (1001, N'Alpha certificate', N'C:\certs\alpha.crt', N'C:\certs\alpha.key');

INSERT INTO dbo.hm_groups (groupid, groupname)
VALUES
    (1200, N'Support'),
    (1100, N'Administrators');

INSERT INTO dbo.hm_group_members (memberid, membergroupid, memberaccountid)
VALUES
    (1500, 1200, 20),
    (1400, 1100, 20),
    (1300, 1100, 10);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainAndAliasSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL,
    domainpostmaster nvarchar(80) NOT NULL,
    domainmaxmessagesize int NOT NULL,
    domainuseplusaddressing tinyint NOT NULL,
    domainplusaddressingchar nvarchar(1) NOT NULL,
    domainaddomain nvarchar(255) NOT NULL,
    domainmaxsize int NOT NULL,
    domainmaxnoofaccounts int NOT NULL,
    domainmaxnoofaliases int NOT NULL,
    domainmaxnoofdistributionlists int NOT NULL,
    domainlimitationsenabled int NOT NULL,
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

CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountmaxsize int NOT NULL
);

CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messageaccountid int NOT NULL,
    messagesize bigint NOT NULL
);

CREATE TABLE dbo.hm_aliases
(
    aliasid int NOT NULL PRIMARY KEY,
    aliasdomainid int NOT NULL,
    aliasname nvarchar(255) NOT NULL,
    aliasvalue nvarchar(255) NOT NULL,
    aliasactive tinyint NOT NULL
);

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainaddomain, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', N'', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', N'', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

INSERT INTO dbo.hm_aliases (aliasid, aliasdomainid, aliasname, aliasvalue, aliasactive)
VALUES
    (20, 10, N'sales@example.test', N'user@example.test', 0),
    (10, 10, N'abuse@example.test', N'admin@example.test', 1),
    (30, 30, N'outside@other.test', N'outside-target@other.test', 1);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainAndDistributionListSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL,
    domainpostmaster nvarchar(80) NOT NULL,
    domainmaxmessagesize int NOT NULL,
    domainuseplusaddressing tinyint NOT NULL,
    domainplusaddressingchar nvarchar(1) NOT NULL,
    domainaddomain nvarchar(255) NOT NULL,
    domainmaxsize int NOT NULL,
    domainmaxnoofaccounts int NOT NULL,
    domainmaxnoofaliases int NOT NULL,
    domainmaxnoofdistributionlists int NOT NULL,
    domainlimitationsenabled int NOT NULL,
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

CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountmaxsize int NOT NULL
);

CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messageaccountid int NOT NULL,
    messagesize bigint NOT NULL
);

CREATE TABLE dbo.hm_distributionlists
(
    distributionlistid int NOT NULL PRIMARY KEY,
    distributionlistdomainid int NOT NULL,
    distributionlistaddress nvarchar(255) NOT NULL,
    distributionlistenabled tinyint NOT NULL,
    distributionlistrequireauth tinyint NOT NULL,
    distributionlistrequireaddress nvarchar(255) NOT NULL,
    distributionlistmode tinyint NOT NULL
);

CREATE TABLE dbo.hm_distributionlistsrecipients
(
    distributionlistrecipientid int NOT NULL PRIMARY KEY,
    distributionlistrecipientlistid int NOT NULL,
    distributionlistrecipientaddress nvarchar(255) NOT NULL
);

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainaddomain, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', N'', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', N'', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

INSERT INTO dbo.hm_distributionlists
    (distributionlistid, distributionlistdomainid, distributionlistaddress, distributionlistenabled,
     distributionlistrequireauth, distributionlistrequireaddress, distributionlistmode)
VALUES
    (20, 10, N'members@example.test', 0, 1, N'owner@example.test', 1),
    (10, 10, N'announce@example.test', 1, 0, N'', 0),
    (30, 30, N'outside@other.test', 1, 0, N'', 0);

INSERT INTO dbo.hm_distributionlistsrecipients
    (distributionlistrecipientid, distributionlistrecipientlistid, distributionlistrecipientaddress)
VALUES
    (200, 20, N'zeta@example.test'),
    (100, 20, N'alpha@example.test'),
    (300, 30, N'outside-member@other.test');
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainAndDomainAliasSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL,
    domainpostmaster nvarchar(80) NOT NULL,
    domainmaxmessagesize int NOT NULL,
    domainuseplusaddressing tinyint NOT NULL,
    domainplusaddressingchar nvarchar(1) NOT NULL,
    domainaddomain nvarchar(255) NOT NULL,
    domainmaxsize int NOT NULL,
    domainmaxnoofaccounts int NOT NULL,
    domainmaxnoofaliases int NOT NULL,
    domainmaxnoofdistributionlists int NOT NULL,
    domainlimitationsenabled int NOT NULL,
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

CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountmaxsize int NOT NULL
);

CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messageaccountid int NOT NULL,
    messagesize bigint NOT NULL
);

CREATE TABLE dbo.hm_domain_aliases
(
    daid int NOT NULL PRIMARY KEY,
    dadomainid int NOT NULL,
    daalias nvarchar(255) NOT NULL
);

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainaddomain, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', N'', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', N'', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

INSERT INTO dbo.hm_domain_aliases (daid, dadomainid, daalias)
VALUES
    (20, 10, N'alias-two.test'),
    (10, 10, N'alias-one.test'),
    (30, 30, N'outside.test');
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

    private sealed class FixedSettingsAdministrationStore : ISettingsAdministrationStore
    {
        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                new SettingsAdministrationSnapshot(
                    HostName: string.Empty,
                    WelcomeSmtp: string.Empty,
                    WelcomePop3: string.Empty,
                    WelcomeImap: string.Empty));
    }

    private sealed class FixedBlockedAttachmentAdministrationStore(
        IReadOnlyList<BlockedAttachmentAdministrationSnapshot> attachments)
        : IBlockedAttachmentAdministrationStore
    {
        public ValueTask<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>> GetBlockedAttachmentsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(attachments);
    }

    private sealed class FixedDnsBlackListAdministrationStore(
        IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists)
        : IDnsBlackListAdministrationStore
    {
        public ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(blackLists);
    }

    private sealed class FixedSurblServerAdministrationStore(
        IReadOnlyList<SurblServerAdministrationSnapshot> servers)
        : ISurblServerAdministrationStore
    {
        public ValueTask<IReadOnlyList<SurblServerAdministrationSnapshot>> GetSurblServersAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(servers);
    }

    private sealed class FixedGreyListingWhiteAddressAdministrationStore(
        IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses)
        : IGreyListingWhiteAddressAdministrationStore
    {
        public ValueTask<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>> GetWhiteAddressesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(addresses);
    }

    private sealed class FixedWhiteListAddressAdministrationStore(
        IReadOnlyList<WhiteListAddressAdministrationSnapshot> addresses)
        : IWhiteListAddressAdministrationStore
    {
        public ValueTask<IReadOnlyList<WhiteListAddressAdministrationSnapshot>> GetWhiteListAddressesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(addresses);
    }

    private sealed class IntegrationDeliveryQueueClearObserver : IDeliveryQueueClearObserver
    {
        public TaskCompletionSource<int> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Completed(int removedMessages) =>
            Completion.TrySetResult(removedMessages);

        public void Failed(Exception exception) =>
            Completion.TrySetException(exception);
    }
}
