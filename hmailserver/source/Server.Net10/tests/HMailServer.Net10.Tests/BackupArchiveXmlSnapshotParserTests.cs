using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

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