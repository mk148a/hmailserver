using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDomainAdministrationStore : IDomainAdministrationStore
{
    private const int AntiSpamOptionDkimSign = 2;
    private const int AntiSpamOptionDkimSimpleHeader = 4;
    private const int AntiSpamOptionDkimSimpleBody = 8;
    private const int AntiSpamOptionDkimSha1 = 16;
    private const int AntiSpamOptionDkimSignAliases = 32;

    public const string GetDomainsSql = """
SELECT
    domainid,
    domainname,
    domainactive,
    domainpostmaster,
    domainmaxmessagesize,
    domainuseplusaddressing,
    domainplusaddressingchar,
    domainmaxsize,
    domainmaxnoofaccounts,
    domainmaxnoofaliases,
    domainmaxnoofdistributionlists,
    domainlimitationsenabled,
    domainmaxaccountsize,
    domainenablesignature,
    domainsignaturemethod,
    domainsignatureplaintext,
    domainsignaturehtml,
    domainaddsignaturestoreplies,
    domainaddsignaturestolocalemail,
    domainantispamoptions,
    domaindkimselector,
    domaindkimprivatekeyfile
FROM hm_domains
ORDER BY domainname ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDomainAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetDomainsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var domains = new List<DomainAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var active = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0;
            var postmaster = reader.GetString(3);
            var maxMessageSize = reader.GetInt32(4);
            var plusAddressingEnabled = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0;
            var plusAddressingCharacter = reader.GetString(6);
            var maxSize = reader.GetInt32(7);
            var maxNumberOfAccounts = reader.GetInt32(8);
            var maxNumberOfAliases = reader.GetInt32(9);
            var maxNumberOfDistributionLists = reader.GetInt32(10);
            var limitationsEnabled = Convert.ToInt32(reader.GetValue(11), CultureInfo.InvariantCulture);
            var maxAccountSize = reader.GetInt32(12);
            var signatureEnabled = Convert.ToInt32(reader.GetValue(13), CultureInfo.InvariantCulture) != 0;
            var signatureMethod = Convert.ToInt32(reader.GetValue(14), CultureInfo.InvariantCulture);
            var signaturePlainText = reader.GetString(15);
            var signatureHtml = reader.GetString(16);
            var addSignaturesToReplies = Convert.ToInt32(reader.GetValue(17), CultureInfo.InvariantCulture) != 0;
            var addSignaturesToLocalMail = Convert.ToInt32(reader.GetValue(18), CultureInfo.InvariantCulture) != 0;
            var antiSpamOptions = Convert.ToInt32(reader.GetValue(19), CultureInfo.InvariantCulture);
            domains.Add(
                new DomainAdministrationSnapshot(
                    Id: id,
                    Name: name,
                    Active: active,
                    Postmaster: postmaster,
                    MaxMessageSize: maxMessageSize,
                    PlusAddressingEnabled: plusAddressingEnabled,
                    PlusAddressingCharacter: plusAddressingCharacter,
                    MaxSize: maxSize,
                    MaxNumberOfAccounts: maxNumberOfAccounts,
                    MaxNumberOfAliases: maxNumberOfAliases,
                    MaxNumberOfDistributionLists: maxNumberOfDistributionLists,
                    MaxNumberOfAccountsEnabled: (limitationsEnabled & 1) != 0,
                    MaxNumberOfAliasesEnabled: (limitationsEnabled & 2) != 0,
                    MaxNumberOfDistributionListsEnabled: (limitationsEnabled & 4) != 0,
                    MaxAccountSize: maxAccountSize,
                    SignatureEnabled: signatureEnabled,
                    SignatureMethod: signatureMethod,
                    SignaturePlainText: signaturePlainText,
                    SignatureHtml: signatureHtml,
                    AddSignaturesToReplies: addSignaturesToReplies,
                    AddSignaturesToLocalMail: addSignaturesToLocalMail,
                    DkimSignEnabled: (antiSpamOptions & AntiSpamOptionDkimSign) != 0,
                    DkimSelector: reader.GetString(20),
                    DkimPrivateKeyFile: reader.GetString(21),
                    DkimHeaderCanonicalizationMethod:
                        (antiSpamOptions & AntiSpamOptionDkimSimpleHeader) != 0 ? 1 : 2,
                    DkimBodyCanonicalizationMethod:
                        (antiSpamOptions & AntiSpamOptionDkimSimpleBody) != 0 ? 1 : 2,
                    DkimSigningAlgorithm:
                        (antiSpamOptions & AntiSpamOptionDkimSha1) != 0 ? 1 : 2,
                    DkimSignAliasesEnabled: (antiSpamOptions & AntiSpamOptionDkimSignAliases) != 0));
        }

        return domains;
    }
}
