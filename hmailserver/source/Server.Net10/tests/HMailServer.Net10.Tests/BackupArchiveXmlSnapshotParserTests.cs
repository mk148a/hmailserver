using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupArchiveXmlSnapshotParserTests
{
    private const string DomainXml = """
        <Backup>
          <Domains>
            <Domain Name="alpha.example"
                    Postmaster="postmaster@alpha.example"
                    Active="1"
                    MaxMessageSize="2048"
                    UsePlusAddressing="1"
                    PlusAddressingChar="+"
                    AntiSpamOptions="5"
                    MaxNoOfAccounts="5"
                    MaxNoOfAliases="3"
                    MaxNoOfLists="2"
                    LimitationsEnabled="3"
                    EnableSignature="1"
                    SignatureMethod="2"
                    AddSignaturesToLocalMail="1"
                    AddSignaturesToReplies="0"
                    DKIMSelector="selector"
                    DKIMPrivateKeyFile="key.pem" />
          </Domains>
        </Backup>
        """;

    private const string AccountXml = """
        <Backup>
          <Domains>
            <Domain Name="d">
              <Accounts>
                <Account Name="a@d.example"
                         PersonFirstName="Ada"
                         PersonLastName="Lovelace"
                         Active="1"
                         Password="encrypted"
                         PasswordEncryption="1"
                         MaxAccountSize="123"
                         ADActive="1"
                         ADDomain="CORP"
                         ADUsername="ada"
                         VacationMessageOn="1"
                         VacationMessage="away"
                         ForwardEnabled="1"
                         ForwardAddress="fwd@example.test"
                         EnableSignature="1"
                         SignaturePlainText="sig"
                         AdminLevel="2" />
              </Accounts>
            </Domain>
          </Domains>
        </Backup>
        """;

    [TestMethod]
    public void ParseAccounts_ReconstructsLegacySnapshotFields()
    {
        var accounts = BackupArchiveXmlSnapshotParser.ParseAccounts(AccountXml, domainId: 7);

        Assert.AreEqual(1, accounts.Count);
        var entry = accounts[0];
        Assert.AreEqual("a@d.example", entry.Account.Address);
        Assert.AreEqual(7, entry.Account.DomainId);
        Assert.IsTrue(entry.Account.Active);
        Assert.AreEqual(2, entry.Account.AdminLevel);
        Assert.AreEqual("encrypted", entry.Password);
        Assert.AreEqual(1, entry.PasswordEncryption);
        Assert.AreEqual(123, entry.Account.MaxSize);
        Assert.IsTrue(entry.Account.IsActiveDirectoryAccount);
        Assert.AreEqual("CORP", entry.Account.ActiveDirectoryDomain);
        Assert.AreEqual("ada", entry.Account.ActiveDirectoryUsername);
        Assert.IsTrue(entry.Account.VacationMessageIsOn);
        Assert.AreEqual("away", entry.Account.VacationMessage);
        Assert.IsTrue(entry.Account.ForwardEnabled);
        Assert.AreEqual("fwd@example.test", entry.Account.ForwardAddress);
        Assert.IsTrue(entry.Account.SignatureEnabled);
        Assert.AreEqual("Ada", entry.Account.PersonFirstName);
    }

    [TestMethod]
    public void ParseSettingsProperties_AllowsAbsentProperties()
    {
        var properties = BackupArchiveXmlSnapshotParser.ParseSettingsProperties("<Backup />");

        Assert.AreEqual(0, properties.Count);
    }

    [TestMethod]
    public void ParseSettingsProperties_PreservesKnownUnknownAndDuplicateNodesInOrder()
    {
        const string xml = """
            <Backup>
              <Properties>
                <relaymode LongValue="2" StringValue="first" />
                <unknown-setting LongValue="7" StringValue="unknown" />
                <relaymode LongValue="3" StringValue="second" />
              </Properties>
            </Backup>
            """;

        var properties = BackupArchiveXmlSnapshotParser.ParseSettingsProperties(xml);

        Assert.AreEqual(3, properties.Count);
        Assert.AreEqual(("relaymode", 2L, "first"),
            (properties[0].Name, properties[0].LongValue, properties[0].StringValue));
        Assert.AreEqual(("unknown-setting", 7L, "unknown"),
            (properties[1].Name, properties[1].LongValue, properties[1].StringValue));
        Assert.AreEqual(("relaymode", 3L, "second"),
            (properties[2].Name, properties[2].LongValue, properties[2].StringValue));
    }

    [TestMethod]
    public void ParseSettingsProperties_UsesLegacyDefaultsForMissingAndInvalidAttributes()
    {
        const string xml = """
            <Backup>
              <Properties>
                <missing-long StringValue="present" />
                <invalid-long LongValue="not-a-number" />
                <missing-both />
              </Properties>
            </Backup>
            """;

        var properties = BackupArchiveXmlSnapshotParser.ParseSettingsProperties(xml);

        Assert.AreEqual(3, properties.Count);
        Assert.AreEqual(0L, properties[0].LongValue);
        Assert.AreEqual("present", properties[0].StringValue);
        Assert.AreEqual(0L, properties[1].LongValue);
        Assert.AreEqual(string.Empty, properties[1].StringValue);
        Assert.AreEqual(0L, properties[2].LongValue);
        Assert.AreEqual(string.Empty, properties[2].StringValue);
    }

    [TestMethod]
    public async Task RestoreAccountsAsync_ReplaysParsedArchiveIntoAccountStore()
    {
        var store = new RecordingAccountStore();
        var entries = BackupArchiveXmlSnapshotParser.ParseAccounts(AccountXml, domainId: 7);

        var result = await BackupRestoreMetadataWriter.RestoreAccountsAsync(
            entries,
            domainId: 7,
            store,
            () => default,
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(1, result.RestoredAccounts);
        Assert.AreEqual(7, store.InsertDomainId);
        Assert.AreEqual(1, store.Inserted.Count);
        Assert.AreEqual("a@d.example", store.Inserted[0].Address);
        Assert.AreEqual("encrypted", store.InsertedPassword);
        Assert.AreEqual(1, store.InsertedPasswordEncryption);
    }

    [TestMethod]
    public async Task RestoreAccountsAsync_PropagatesArchivePasswordAndEncryptionType()
    {
        var account = new AccountAdministrationSnapshot(
            Id: 0,
            DomainId: 7,
            Address: "a@d.example",
            Active: true,
            AdminLevel: 0);
        var entries = new[]
        {
            new RestoreAccountEntry(account, "encrypted-archive-value", 1),
            new RestoreAccountEntry(account with { Address = "b@d.example" }, "plain-archive-value", 0)
        };
        var store = new RecordingAccountStore();

        await BackupRestoreMetadataWriter.RestoreAccountsAsync(
            entries,
            domainId: 7,
            store,
            () => default,
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(2, store.InsertedCredentials.Count);
        Assert.AreEqual(("encrypted-archive-value", 1), store.InsertedCredentials[0]);
        Assert.AreEqual(("plain-archive-value", 0), store.InsertedCredentials[1]);
    }

    [TestMethod]
    public void ParseDomainEntries_ReconstructsFetchAccountsAndNestedUids()
    {
        var encryptedPassword = LegacyBlowfishPasswordCipher.Encrypt("fetch-secret");
        var xml = $"""
            <Backup>
              <Domains>
                <Domain Name="d">
                  <Accounts>
                    <Account Name="a@d.example">
                      <FetchAccounts>
                        <FetchAccount Name="fetch" ServerAddress="pop3.example" ServerType="0"
                                      Port="995" Username="fetch-user" Password="{encryptedPassword}"
                                      Minutes="15" DaysToKeep="30" Active="1" MIMERecipientHeaders="To"
                                      ProcessMIMERecipients="1" ProcessMIMEDate="0" UseAntiSpam="1"
                                      UseAntiVirus="0" EnableRouteRecipients="1" ConnectionSecurity="1">
                          <FetchAccountUIDs>
                            <UID UID="uid-1" Date="2026-07-01 12:30:00" />
                            <UID UID="uid-2" Date="2026-07-02 12:30:00" />
                          </FetchAccountUIDs>
                        </FetchAccount>
                      </FetchAccounts>
                    </Account>
                  </Accounts>
                </Domain>
              </Domains>
            </Backup>
            """;

        var entry = BackupArchiveXmlSnapshotParser.ParseDomainEntries(xml).Single().Accounts.Single();
        var fetch = entry.FetchAccounts.Single();

        Assert.AreEqual("fetch", fetch.Account.Name);
        Assert.AreEqual("pop3.example", fetch.Account.ServerAddress);
        Assert.AreEqual(995, fetch.Account.Port);
        Assert.AreEqual(15, fetch.Account.MinutesBetweenFetch);
        Assert.IsTrue(fetch.Account.ProcessMimeRecipients);
        Assert.IsTrue(fetch.Account.UseAntiSpam);
        Assert.AreEqual(encryptedPassword, fetch.EncryptedPassword);
        Assert.AreEqual(2, fetch.Uids.Count);
        Assert.AreEqual("uid-1", fetch.Uids[0].Value);
        Assert.AreEqual("2026-07-01 12:30:00", fetch.Uids[0].Date);
    }

    [TestMethod]
    public void ParseDomainEntries_ReconstructsLegacyRuleCriteriaAndActions()
    {
        const string xml = """
            <Backup>
              <Domains>
                <Domain Name="d">
                  <Accounts>
                    <Account Name="a@d.example">
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
                    </Account>
                  </Accounts>
                </Domain>
              </Domains>
            </Backup>
            """;

        var rule = BackupArchiveXmlSnapshotParser.ParseDomainEntries(xml)
            .Single().Accounts.Single().Rules.Single();

        Assert.AreEqual("subject rule", rule.Rule.Name);
        Assert.IsTrue(rule.Rule.Active);
        Assert.IsTrue(rule.Rule.UseAnd);
        Assert.AreEqual(2, rule.Rule.SortOrder);
        Assert.AreEqual("needle", rule.Criteria.Single().MatchValue);
        Assert.AreEqual(1, rule.Criteria.Single().PredefinedField);
        Assert.AreEqual("to@example.test", rule.Actions.Single().To);
        Assert.AreEqual("INBOX.processed", rule.Actions.Single().ImapFolder);
        Assert.AreEqual(4, rule.Actions.Single().RouteId);
        Assert.IsTrue(rule.Actions.Single().AbortSpamFlagged);
    }

    [TestMethod]
    public void ParseDomainEntries_ReconstructsFolderAndMessageMetadataAndRejectsPermissions()
    {
        const string xml = """
            <Backup>
              <Domains>
                <Domain Name="d">
                  <Accounts>
                    <Account Name="a@d.example">
                      <Folders>
                        <Folder Name="INBOX" Subscribed="1" CreateTime="2026-07-01 12:30:00" CurrentUID="5">
                          <Folders>
                            <Folder Name="child" Subscribed="0" CreateTime="2026-07-01 12:31:00" CurrentUID="2" />
                          </Folders>
                        </Folder>
                      </Folders>
                    </Account>
                  </Accounts>
                </Domain>
              </Domains>
            </Backup>
            """;

        var folder = BackupArchiveXmlSnapshotParser.ParseDomainEntries(xml)
            .Single().Accounts.Single().Folders.Single();
        Assert.AreEqual("INBOX", folder.Folder.Name);
        Assert.AreEqual(5, folder.Folder.CurrentUid);
        Assert.AreEqual("child", folder.Children.Single().Folder.Name);

        var withMessages = xml.Replace(
            "CurrentUID=\"5\">",
            "CurrentUID=\"5\"><Messages><Message CreateTime=\"2026-07-01 12:32:00\" Filename=\"one.eml\" FromAddress=\"sender@example.test\" State=\"2\" Size=\"42\" NoOfRetries=\"9\" Flags=\"1\" ID=\"77\" UID=\"8\" /></Messages>",
            StringComparison.Ordinal);
        var message = BackupArchiveXmlSnapshotParser.ParseDomainEntries(withMessages)
            .Single().Accounts.Single().Folders.Single().Messages.Single();
        Assert.AreEqual("one.eml", message.FileName);
        Assert.AreEqual(42, message.SizeBytes);
        Assert.AreEqual(1, message.Flags);
        Assert.AreEqual(8, message.Uid);
        Assert.AreEqual(9, message.CurrentNumberOfTries);

        var withPermissions = xml.Replace(
            "CurrentUID=\"2\" />",
            "CurrentUID=\"2\"><Permissions /></Folder>",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(() => BackupArchiveXmlSnapshotParser.ParseDomainEntries(withPermissions));
    }

    [TestMethod]
    public void ParsePublicFolderEntries_PreservesLegacyAclHolderFieldsOrderAndDuplicates()
    {
        const string xml = """
            <Backup>
              <PublicFolders>
                <Folder Name="Shared" Subscribed="0" CreateTime="2026-07-01 12:30:00" CurrentUID="4">
                  <ACLs>
                    <Permission Type="0" Rights="3" Holder="user@example.test" />
                    <Permission Type="1" Rights="1024" Holder="Editors" />
                    <Permission Type="2" Rights="2047" Holder="Anyone" />
                    <Permission Type="0" Rights="3" Holder="user@example.test" />
                  </ACLs>
                </Folder>
              </PublicFolders>
            </Backup>
            """;

        var folder = BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(xml).Single();

        Assert.AreEqual(0, folder.Folder.AccountId);
        Assert.AreEqual("Shared", folder.Folder.Name);
        Assert.AreEqual(4, folder.Permissions.Count);
        Assert.AreEqual(0, folder.Permissions[0].PermissionType);
        Assert.AreEqual(3, folder.Permissions[0].Rights);
        Assert.AreEqual("user@example.test", folder.Permissions[0].Holder);
        Assert.AreEqual(1, folder.Permissions[1].PermissionType);
        Assert.AreEqual("Editors", folder.Permissions[1].Holder);
        Assert.AreEqual(2, folder.Permissions[2].PermissionType);
        Assert.AreEqual("Anyone", folder.Permissions[2].Holder);
        Assert.AreEqual("user@example.test", folder.Permissions[3].Holder);
    }

    [TestMethod]
    public void ParsePublicFolderEntries_RejectsMalformedLegacyAclFields()
    {
        const string prefix = "<Backup><PublicFolders><Folder Name=\"Shared\" Subscribed=\"0\" CreateTime=\"2026-07-01 12:30:00\" CurrentUID=\"4\"><ACLs><Permission ";
        const string suffix = " /></ACLs></Folder></PublicFolders></Backup>";

        Assert.ThrowsExactly<InvalidDataException>(() =>
            BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(
                prefix + "Type=\"9\" Rights=\"1\" Holder=\"Anyone\"" + suffix));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(
                prefix + "Type=\"0\" Rights=\"2048\" Holder=\"user@example.test\"" + suffix));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(
                prefix + "Type=\"0\" Rights=\"1\" Holder=\"\"" + suffix));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(
                prefix + "Type=\"0\" Rights=\"not-a-number\" Holder=\"user@example.test\"" + suffix));
    }

    [TestMethod]
    public void ParsePublicFolderEntries_RejectsUnexpectedAclChildrenAndDuplicateContainers()
    {
        const string baseFolder = "<Backup><PublicFolders><Folder Name=\"Shared\" Subscribed=\"0\" CreateTime=\"2026-07-01 12:30:00\" CurrentUID=\"4\">";

        Assert.ThrowsExactly<InvalidDataException>(() =>
            BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(
                baseFolder + "<ACLs><Wrong /></ACLs></Folder></PublicFolders></Backup>"));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(
                baseFolder + "<ACLs /><ACLs /></Folder></PublicFolders></Backup>"));
    }

    [TestMethod]
    public async Task RestoreFetchAccountsAsync_PreservesArchiveCiphertextAndRestoresUids()
    {
        var encryptedPassword = LegacyBlowfishPasswordCipher.Encrypt("fetch-secret");
        var store = new RecordingFetchAccountStore();
        var entries = new[]
        {
            new RestoreFetchAccountEntry(
                new FetchAccountAdministrationDraft(AccountId: 0, Name: "fetch"),
                encryptedPassword,
                new[] { new FetchAccountUidBackupAdministrationSnapshot("uid-1", "2026-07-01 12:30:00") })
        };

        var result = await BackupRestoreMetadataWriter.RestoreFetchAccountsAsync(
            entries,
            accountId: 42,
            store,
            () => default,
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(1, result.RestoredFetchAccounts);
        Assert.AreEqual(1, result.RestoredFetchAccountUids);
        Assert.AreEqual(42, store.Inserted[0].AccountId);
        Assert.AreEqual(encryptedPassword, store.Inserted[0].Password);
        Assert.AreEqual((7, "uid-1", "2026-07-01 12:30:00"), store.InsertedUids[0]);
    }

    private sealed class RecordingAccountStore : IAccountAdministrationStore
    {
        public int InsertDomainId { get; private set; }
        public List<AccountAdministrationSnapshot> Inserted { get; } = new();
        public string? InsertedPassword { get; private set; }
        public int InsertedPasswordEncryption { get; private set; }
        public List<(string Password, int PasswordEncryption)> InsertedCredentials { get; } = new();

        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(Array.Empty<AccountAdministrationSnapshot>());

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(int accountId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<AccountAdministrationSnapshot?>(null);

        public ValueTask<int> InsertAccountAsync(int domainId, AccountAdministrationSnapshot account, string password, CancellationToken cancellationToken)
        {
            InsertDomainId = domainId;
            Inserted.Add(account);
            InsertedPassword = password;
            return ValueTask.FromResult(Inserted.Count);
        }

        public ValueTask<int> InsertAccountForRestoreAsync(
            int domainId,
            AccountAdministrationSnapshot account,
            string password,
            int passwordEncryption,
            CancellationToken cancellationToken)
        {
            InsertDomainId = domainId;
            Inserted.Add(account);
            InsertedPassword = password;
            InsertedPasswordEncryption = passwordEncryption;
            InsertedCredentials.Add((password, passwordEncryption));
            return ValueTask.FromResult(Inserted.Count);
        }
    }

    private sealed class RecordingFetchAccountStore : IFetchAccountAdministrationStore
    {
        public List<FetchAccountAdministrationDraft> Inserted { get; } = new();
        public List<(int FetchAccountId, string Value, string Date)> InsertedUids { get; } = new();

        public ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<FetchAccountAdministrationSnapshot>>(Array.Empty<FetchAccountAdministrationSnapshot>());

        public ValueTask SetRetryNowAsync(int accountId, int fetchAccountId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<int> InsertFetchAccountAsync(FetchAccountAdministrationDraft account, CancellationToken cancellationToken)
        {
            Inserted.Add(account);
            return ValueTask.FromResult(7);
        }

        public ValueTask<int> InsertFetchAccountForRestoreAsync(
            FetchAccountAdministrationDraft account,
            string encryptedPassword,
            CancellationToken cancellationToken)
        {
            Inserted.Add(account with { Password = encryptedPassword });
            return ValueTask.FromResult(7);
        }

        public ValueTask InsertFetchAccountUidAsync(
            int fetchAccountId,
            string uidValue,
            string uidTime,
            CancellationToken cancellationToken)
        {
            InsertedUids.Add((fetchAccountId, uidValue, uidTime));
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteFetchAccountAsync(int accountId, int fetchAccountId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private const string AliasAndListXml = """
        <Backup>
          <Domains>
            <Domain Name="d">
              <Aliases>
                <Alias Name="alias@d.example" Value="target@example.test" Active="1" />
              </Aliases>
              <DistributionLists>
                <DistributionList Name="team@d.example" Active="1" RequiresAuth="1"
                                  RequiresAuthAddress="sender@example.test" ListMode="1" />
              </DistributionLists>
            </Domain>
          </Domains>
        </Backup>
        """;

    [TestMethod]
    public void ParseAliases_ReconstructsLegacySnapshotFields()
    {
        var aliases = BackupArchiveXmlSnapshotParser.ParseAliases(AliasAndListXml, domainId: 7);

        Assert.AreEqual(1, aliases.Count);
        Assert.AreEqual("alias@d.example", aliases[0].Name);
        Assert.AreEqual("target@example.test", aliases[0].Value);
        Assert.IsTrue(aliases[0].Active);
        Assert.AreEqual(7, aliases[0].DomainId);
    }

    [TestMethod]
    public void ParseDistributionLists_ReconstructsLegacySnapshotFields()
    {
        var lists = BackupArchiveXmlSnapshotParser.ParseDistributionLists(AliasAndListXml, domainId: 7);

        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual("team@d.example", lists[0].Address);
        Assert.IsTrue(lists[0].Active);
        Assert.IsTrue(lists[0].RequireSmtpAuth);
        Assert.AreEqual("sender@example.test", lists[0].RequireSenderAddress);
        Assert.AreEqual(1, lists[0].Mode);
        Assert.AreEqual(7, lists[0].DomainId);
    }

    private const string RecipientXml = """
        <Backup>
          <Domains>
            <Domain Name="d">
              <DistributionLists>
                <DistributionList Name="team@d.example">
                  <Recipients>
                    <Recipient Name="r1@example.test" />
                    <Recipient Name="r2@example.test" />
                  </Recipients>
                </DistributionList>
              </DistributionLists>
            </Domain>
          </Domains>
        </Backup>
        """;

    [TestMethod]
    public void ParseDistributionListRecipients_ReconstructsLegacySnapshotFields()
    {
        var recipients = BackupArchiveXmlSnapshotParser.ParseDistributionListRecipients(RecipientXml, distributionListId: 42);

        Assert.AreEqual(2, recipients.Count);
        Assert.AreEqual(42, recipients[0].ListId);
        Assert.AreEqual("r1@example.test", recipients[0].Address);
        Assert.AreEqual("r2@example.test", recipients[1].Address);
    }

    [TestMethod]
    public void ParseDomains_ReconstructsLegacySnapshotFields()
    {
        var domains = BackupArchiveXmlSnapshotParser.ParseDomains(DomainXml);

        Assert.AreEqual(1, domains.Count);
        var domain = domains[0];
        Assert.AreEqual(0, domain.Id);
        Assert.AreEqual("alpha.example", domain.Name);
        Assert.IsTrue(domain.Active);
        Assert.AreEqual("postmaster@alpha.example", domain.Postmaster);
        Assert.AreEqual(2048, domain.MaxMessageSize);
        Assert.IsTrue(domain.PlusAddressingEnabled);
        Assert.AreEqual(5, domain.MaxNumberOfAccounts);
        Assert.AreEqual("selector", domain.DkimSelector);
        Assert.IsTrue(domain.AddSignaturesToLocalMail);
        Assert.IsFalse(domain.AddSignaturesToReplies);

        Assert.IsTrue(domain.AntiSpamEnableGreylisting);
        Assert.AreEqual(1, domain.DkimHeaderCanonicalizationMethod);
        Assert.AreEqual(2, domain.DkimBodyCanonicalizationMethod);
        Assert.AreEqual(2, domain.DkimSigningAlgorithm);
        Assert.IsFalse(domain.DkimSignEnabled);

        Assert.IsTrue(domain.MaxNumberOfAccountsEnabled);
        Assert.IsTrue(domain.MaxNumberOfAliasesEnabled);
        Assert.IsFalse(domain.MaxNumberOfDistributionListsEnabled);
    }

    [TestMethod]
    public async Task RestoreDomainsAsync_ReplaysParsedArchiveIntoStore()
    {
        var store = new RecordingDomainStore();
        var domains = BackupArchiveXmlSnapshotParser.ParseDomains(DomainXml);

        var result = await BackupRestoreMetadataWriter.RestoreDomainsAsync(
            domains,
            store,
            () => default,
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(1, result.RestoredDomains);
        Assert.AreEqual(1, store.Inserted.Count);
        Assert.AreEqual("alpha.example", store.Inserted[0].Name);
        Assert.IsTrue(store.Inserted[0].PlusAddressingEnabled);
    }

    private sealed class RecordingDomainStore : IDomainAdministrationStore
    {
        public List<DomainAdministrationSnapshot> Inserted { get; } = new();

        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>(Array.Empty<DomainAdministrationSnapshot>());

        public ValueTask<int> InsertDomainAsync(DomainAdministrationSnapshot domain, CancellationToken cancellationToken)
        {
            Inserted.Add(domain);
            return ValueTask.FromResult(Inserted.Count);
        }
    }
}
