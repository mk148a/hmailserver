using HMailServer.ComInterop;
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
            Assert.AreEqual("alpha.example", domains[0].Name);
            Assert.AreEqual("postmaster@alpha.example", domains[0].Postmaster);
            Assert.AreEqual(1024, domains[0].MaxMessageSize);
            Assert.IsTrue(domains[0].PlusAddressingEnabled);
            Assert.AreEqual("+", domains[0].PlusAddressingCharacter);
            Assert.IsTrue(domains[0].AntiSpamEnableGreylisting);
            Assert.AreEqual(4096, domains[0].MaxSize);
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
            FetchAccountAdministrationRuntimeHost.Configure(new SqlServerFetchAccountAdministrationStore(connectionFactory));
            RuleAdministrationRuntimeHost.Configure(new SqlServerRuleAdministrationStore(connectionFactory));
            ImapFolderAdministrationRuntimeHost.Configure(new SqlServerImapFolderAdministrationStore(connectionFactory));
            RouteAdministrationRuntimeHost.Configure(new SqlServerRouteAdministrationStore(connectionFactory));
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
            Assert.AreEqual(2048, accounts[0].MaxSize);
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
            var rules = accounts[0].Rules;
            Assert.AreEqual(2, rules.Count);
            Assert.AreEqual("First rule", rules[0].Name);
            Assert.AreEqual(10, rules[0].AccountID);
            Assert.IsTrue(rules[0].Active);
            Assert.IsTrue(rules[0].UseAND);
            Assert.AreEqual("Second rule", rules.get_ItemByDBID(300).Name);
            Assert.IsFalse(rules.get_ItemByDBID(300).Active);
            var pendingCriterias = Assert.ThrowsExactly<COMException>(() => _ = rules[0].Criterias);
            Assert.AreEqual(unchecked((int)0x80004001), pendingCriterias.ErrorCode);
            var globalRules = application.Rules;
            Assert.AreEqual(2, globalRules.Count);
            Assert.AreEqual("Global first", globalRules[0].Name);
            Assert.AreEqual(0, globalRules[0].AccountID);
            Assert.IsTrue(globalRules[0].Active);
            Assert.IsTrue(globalRules[0].UseAND);
            Assert.AreEqual("Global second", globalRules.get_ItemByDBID(150).Name);
            Assert.IsFalse(globalRules.get_ItemByDBID(150).Active);
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
            var pendingMessages = Assert.ThrowsExactly<COMException>(() => _ = folders[0].Messages);
            Assert.AreEqual(unchecked((int)0x80004001), pendingMessages.ErrorCode);
            var publicFolders = application.Settings.PublicFolders;
            Assert.AreEqual(1, publicFolders.Count);
            Assert.AreEqual(50, publicFolders[0].ID);
            Assert.AreEqual("Public", publicFolders[0].Name);
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
            var pendingRouteAddresses = Assert.ThrowsExactly<COMException>(() => _ = routes[0].Addresses);
            Assert.AreEqual(unchecked((int)0x80004001), pendingRouteAddresses.ErrorCode);
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

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (20, N'beta.example', 0, N'postmaster@beta.example', 512, 0, N'+', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (10, N'alpha.example', 1, N'postmaster@alpha.example', 1024, 1, N'+', 4096, 200, 30, 12, 5, 512, 1, 3, N'Alpha plain signature', N'<p>Alpha HTML signature</p>', 1, 0, 55, N'alpha-selector', N'C:\keys\alpha.pem');
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
    accountsignaturehtml nvarchar(max) NOT NULL
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
     domainuseplusaddressing, domainplusaddressingchar, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 54, N'example-selector', N'C:\keys\example.pem'),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

INSERT INTO dbo.hm_accounts
    (accountid, accountdomainid, accountaddress, accountactive, accountadminlevel,
     accountmaxsize, accountpersonfirstname, accountpersonlastname,
     accountvacationmessageon, accountvacationmessage, accountvacationsubject,
     accountvacationexpires, accountvacationexpiredate, accountvacationabortspamflagged,
     accountforwardenabled, accountforwardaddress, accountforwardkeeporiginal,
     accountforwardabortspamflagged, accountenablesignature, accountsignatureplaintext,
     accountsignaturehtml)
VALUES
    (20, 10, N'user@example.test', 0, 0, 1024, N'Grace', N'Hopper',
     0, N'', N'', 0, CONVERT(datetime, '2026-01-01T00:00:00', 126), 0,
     0, N'', 0, 0, 0, N'', N''),
    (10, 10, N'admin@example.test', 1, 2, 2048, N'Ada', N'Lovelace',
     1, N'Away until Monday', N'Auto reply', 1, CONVERT(datetime, '2026-12-31T00:00:00', 126), 1,
     1, N'archive@example.test', 1, 1, 1, N'Regards,' + CHAR(13) + CHAR(10) + N'Ada', N'<p>Regards,<br>Ada</p>'),
    (30, 30, N'outside@other.test', 1, 0, 512, N'Outside', N'Example',
     0, N'', N'', 0, CONVERT(datetime, '2026-01-01T00:00:00', 126), 0,
     0, N'', 0, 0, 0, N'', N'');

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

INSERT INTO dbo.hm_imapfolders
    (folderid, folderaccountid, folderparentid, foldername, folderissubscribed,
     foldercreationtime, foldercurrentuid)
VALUES
    (50, 0, -1, N'Public', 1, CONVERT(datetime, '2026-06-27T00:02:03', 126), 5),
    (100, 10, -1, N'Inbox', 1, CONVERT(datetime, '2026-06-27T01:02:03', 126), 42),
    (200, 10, 100, N'Child', 1, CONVERT(datetime, '2026-06-27T01:03:03', 126), 3),
    (300, 10, -1, N'TE&AOUA5AD2-ST', 0, CONVERT(datetime, '2026-06-26T04:05:06', 126), 7),
    (400, 20, -1, N'User Inbox', 1, CONVERT(datetime, '2026-06-27T01:02:03', 126), 1);

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
     domainuseplusaddressing, domainplusaddressingchar, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

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
     domainuseplusaddressing, domainplusaddressingchar, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

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

CREATE TABLE dbo.hm_domain_aliases
(
    daid int NOT NULL PRIMARY KEY,
    dadomainid int NOT NULL,
    daalias nvarchar(255) NOT NULL
);

INSERT INTO dbo.hm_domains
    (domainid, domainname, domainactive, domainpostmaster, domainmaxmessagesize,
     domainuseplusaddressing, domainplusaddressingchar, domainmaxsize,
     domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
     domainlimitationsenabled, domainmaxaccountsize, domainenablesignature,
     domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
     domainaddsignaturestoreplies, domainaddsignaturestolocalemail, domainantispamoptions,
     domaindkimselector, domaindkimprivatekeyfile)
VALUES
    (10, N'example.test', 1, N'postmaster@example.test', 1024, 1, N'+', 4096, 200, 30, 12, 5, 512, 0, 1, N'', N'', 0, 1, 0, N'', N''),
    (30, N'other.test', 1, N'postmaster@other.test', 512, 0, N'+', 2048, 50, 10, 5, 0, 256, 0, 1, N'', N'', 0, 1, 0, N'', N'');

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
}
