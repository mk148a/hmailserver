using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDomainAdministrationStore : IDomainAdministrationStore
{
    private const int AntiSpamOptionUseGreylisting = 1;
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
    domainaddomain,
    (
        SELECT COALESCE(SUM(CAST(accountmaxsize AS bigint)), 0)
        FROM hm_accounts
        WHERE accountdomainid = hm_domains.domainid
    ) AS domainallocatedsize,
    (
        -- Preserve the legacy MSSQL Domain.Size subquery shape.
        SELECT COALESCE(SUM(CAST(messagesize AS bigint)), 0)
        FROM hm_messages
        WHERE messageaccountid IN
        (
            SELECT accountdomainid
            FROM hm_accounts
            WHERE accountdomainid = hm_domains.domainid
        )
    ) AS domainsizebytes,
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


    public const string InsertDomainSql = """
        INSERT INTO hm_domains
            (domainname, domainactive, domainpostmaster, domainmaxsize, domainaddomain,
             domainmaxmessagesize, domainmaxaccountsize, domainuseplusaddressing,
             domainplusaddressingchar, domainantispamoptions, domainenablesignature,
             domainsignaturemethod, domainsignatureplaintext, domainsignaturehtml,
             domainaddsignaturestoreplies, domainaddsignaturestolocalemail,
             domainmaxnoofaccounts, domainmaxnoofaliases, domainmaxnoofdistributionlists,
             domainlimitationsenabled, domaindkimselector, domaindkimprivatekeyfile)
        OUTPUT INSERTED.domainid
        VALUES
            (@Name, @Active, @Postmaster, @MaxSize, @ADDomain,
             @MaxMessageSize, @MaxAccountSize, @PlusAddressingEnabled,
             @PlusAddressingCharacter, @AntiSpamOptions, @SignatureEnabled,
             @SignatureMethod, @SignaturePlainText, @SignatureHtml,
             @AddSignaturesToReplies, @AddSignaturesToLocalMail,
             @MaxNumberOfAccounts, @MaxNumberOfAliases, @MaxNumberOfDistributionLists,
             @LimitationsEnabled, @DkimSelector, @DkimPrivateKeyFile);
        """;

    public const string UpdateDomainSql = """
        UPDATE hm_domains
        SET domainname = @Name,
            domainactive = @Active,
            domainpostmaster = @Postmaster,
            domainmaxsize = @MaxSize,
            domainaddomain = @ADDomain,
            domainmaxmessagesize = @MaxMessageSize,
            domainmaxaccountsize = @MaxAccountSize,
            domainuseplusaddressing = @PlusAddressingEnabled,
            domainplusaddressingchar = @PlusAddressingCharacter,
            domainantispamoptions = @AntiSpamOptions,
            domainenablesignature = @SignatureEnabled,
            domainsignaturemethod = @SignatureMethod,
            domainsignatureplaintext = @SignaturePlainText,
            domainsignaturehtml = @SignatureHtml,
            domainaddsignaturestoreplies = @AddSignaturesToReplies,
            domainaddsignaturestolocalemail = @AddSignaturesToLocalMail,
            domainmaxnoofaccounts = @MaxNumberOfAccounts,
            domainmaxnoofaliases = @MaxNumberOfAliases,
            domainmaxnoofdistributionlists = @MaxNumberOfDistributionLists,
            domainlimitationsenabled = @LimitationsEnabled,
            domaindkimselector = @DkimSelector,
            domaindkimprivatekeyfile = @DkimPrivateKeyFile
        WHERE domainid = @ID;
        """;

    public const string DeleteDomainByIdSql = """
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        DECLARE @Deleted bit = 0;
        IF EXISTS (SELECT 1 FROM hm_domains WITH (UPDLOCK, HOLDLOCK) WHERE domainid = @ID)
        BEGIN
            DELETE FROM hm_domain_aliases WHERE dadomainid = @ID;
            DELETE FROM hm_distributionlistsrecipients
                WHERE distributionlistrecipientlistid IN (
                    SELECT distributionlistid FROM hm_distributionlists WHERE distributionlistdomainid = @ID);
            DELETE FROM hm_distributionlists WHERE distributionlistdomainid = @ID;
            DELETE FROM hm_aliases WHERE aliasdomainid = @ID;
            DELETE FROM hm_rule_actions
                WHERE actionruleid IN (
                    SELECT ruleid FROM hm_rules
                    WHERE ruleaccountid IN (SELECT accountid FROM hm_accounts WHERE accountdomainid = @ID));
            DELETE FROM hm_rule_criterias
                WHERE criteriaruleid IN (
                    SELECT ruleid FROM hm_rules
                    WHERE ruleaccountid IN (SELECT accountid FROM hm_accounts WHERE accountdomainid = @ID));
            DELETE FROM hm_rules
                WHERE ruleaccountid IN (SELECT accountid FROM hm_accounts WHERE accountdomainid = @ID);
            DELETE FROM hm_messagerecipients
                WHERE recipientmessageid IN (
                    SELECT messageid FROM hm_messages
                    WHERE messageaccountid IN (SELECT accountid FROM hm_accounts WHERE accountdomainid = @ID));
            DELETE FROM hm_message_metadata
                WHERE metadata_accountid IN (SELECT accountid FROM hm_accounts WHERE accountdomainid = @ID);
            DELETE FROM hm_messages
                WHERE messageaccountid IN (SELECT accountid FROM hm_accounts WHERE accountdomainid = @ID);
            DELETE FROM hm_accounts WHERE accountdomainid = @ID;
            DELETE FROM hm_domains WHERE domainid = @ID;
            IF @@ROWCOUNT = 1 SET @Deleted = 1;
        END;

        IF @Deleted = 1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
        SELECT @Deleted;
        """;
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerDomainAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerDomainAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    public async ValueTask<bool> DeleteDomainByIdAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteDomainByIdSql, connection);
        command.Parameters.Add("@ID", SqlDbType.Int).Value = domainId;
        var deleted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return deleted is not null && Convert.ToInt32(deleted, CultureInfo.InvariantCulture) != 0;
    }
    public async ValueTask<bool> UpdateDomainAsync(
        DomainAdministrationSnapshot domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateDomainSql, connection);
        command.Parameters.Add("@ID", SqlDbType.Int).Value = domain.Id;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = domain.Name;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = domain.Active ? 1 : 0;
        command.Parameters.Add("@Postmaster", SqlDbType.NVarChar, 255).Value = domain.Postmaster;
        command.Parameters.Add("@MaxSize", SqlDbType.Int).Value = domain.MaxSize;
        command.Parameters.Add("@ADDomain", SqlDbType.NVarChar, 255).Value = domain.AdDomainName;
        command.Parameters.Add("@MaxMessageSize", SqlDbType.Int).Value = domain.MaxMessageSize;
        command.Parameters.Add("@MaxAccountSize", SqlDbType.Int).Value = domain.MaxAccountSize;
        command.Parameters.Add("@PlusAddressingEnabled", SqlDbType.TinyInt).Value = domain.PlusAddressingEnabled ? 1 : 0;
        command.Parameters.Add("@PlusAddressingCharacter", SqlDbType.NVarChar, 1).Value = domain.PlusAddressingCharacter;
        command.Parameters.Add("@AntiSpamOptions", SqlDbType.Int).Value =
            (domain.AntiSpamEnableGreylisting ? AntiSpamOptionUseGreylisting : 0)
            | (domain.DkimSignEnabled ? AntiSpamOptionDkimSign : 0)
            | (domain.DkimHeaderCanonicalizationMethod == 1 ? AntiSpamOptionDkimSimpleHeader : 0)
            | (domain.DkimBodyCanonicalizationMethod == 1 ? AntiSpamOptionDkimSimpleBody : 0)
            | (domain.DkimSigningAlgorithm == 1 ? AntiSpamOptionDkimSha1 : 0)
            | (domain.DkimSignAliasesEnabled ? AntiSpamOptionDkimSignAliases : 0);
        command.Parameters.Add("@SignatureEnabled", SqlDbType.TinyInt).Value = domain.SignatureEnabled ? 1 : 0;
        command.Parameters.Add("@SignatureMethod", SqlDbType.TinyInt).Value = domain.SignatureMethod;
        command.Parameters.Add("@SignaturePlainText", SqlDbType.NVarChar, -1).Value = domain.SignaturePlainText;
        command.Parameters.Add("@SignatureHtml", SqlDbType.NVarChar, -1).Value = domain.SignatureHtml;
        command.Parameters.Add("@AddSignaturesToReplies", SqlDbType.TinyInt).Value = domain.AddSignaturesToReplies ? 1 : 0;
        command.Parameters.Add("@AddSignaturesToLocalMail", SqlDbType.TinyInt).Value = domain.AddSignaturesToLocalMail ? 1 : 0;
        command.Parameters.Add("@MaxNumberOfAccounts", SqlDbType.Int).Value = domain.MaxNumberOfAccounts;
        command.Parameters.Add("@MaxNumberOfAliases", SqlDbType.Int).Value = domain.MaxNumberOfAliases;
        command.Parameters.Add("@MaxNumberOfDistributionLists", SqlDbType.Int).Value = domain.MaxNumberOfDistributionLists;
        command.Parameters.Add("@LimitationsEnabled", SqlDbType.TinyInt).Value =
            (domain.MaxNumberOfAccountsEnabled ? 1 : 0)
            | (domain.MaxNumberOfAliasesEnabled ? 2 : 0)
            | (domain.MaxNumberOfDistributionListsEnabled ? 4 : 0);
        command.Parameters.Add("@DkimSelector", SqlDbType.NVarChar, 255).Value = domain.DkimSelector;
        command.Parameters.Add("@DkimPrivateKeyFile", SqlDbType.NVarChar, 255).Value = domain.DkimPrivateKeyFile;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
    public async ValueTask<int> InsertDomainAsync(
        DomainAdministrationSnapshot domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(_connectionFactory, _transactionContext, InsertDomainSql, cancellationToken)
            .ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = domain.Name;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = domain.Active ? 1 : 0;
        command.Parameters.Add("@Postmaster", SqlDbType.NVarChar, 255).Value = domain.Postmaster;
        command.Parameters.Add("@MaxSize", SqlDbType.Int).Value = domain.MaxSize;
        command.Parameters.Add("@ADDomain", SqlDbType.NVarChar, 255).Value = domain.AdDomainName;
        command.Parameters.Add("@MaxMessageSize", SqlDbType.Int).Value = domain.MaxMessageSize;
        command.Parameters.Add("@MaxAccountSize", SqlDbType.Int).Value = domain.MaxAccountSize;
        command.Parameters.Add("@PlusAddressingEnabled", SqlDbType.TinyInt).Value = domain.PlusAddressingEnabled ? 1 : 0;
        command.Parameters.Add("@PlusAddressingCharacter", SqlDbType.NVarChar, 1).Value = domain.PlusAddressingCharacter;
        command.Parameters.Add("@AntiSpamOptions", SqlDbType.Int).Value =
            (domain.AntiSpamEnableGreylisting ? AntiSpamOptionUseGreylisting : 0)
            | (domain.DkimSignEnabled ? AntiSpamOptionDkimSign : 0)
            | (domain.DkimHeaderCanonicalizationMethod == 1 ? AntiSpamOptionDkimSimpleHeader : 0)
            | (domain.DkimBodyCanonicalizationMethod == 1 ? AntiSpamOptionDkimSimpleBody : 0)
            | (domain.DkimSigningAlgorithm == 1 ? AntiSpamOptionDkimSha1 : 0)
            | (domain.DkimSignAliasesEnabled ? AntiSpamOptionDkimSignAliases : 0);
        command.Parameters.Add("@SignatureEnabled", SqlDbType.TinyInt).Value = domain.SignatureEnabled ? 1 : 0;
        command.Parameters.Add("@SignatureMethod", SqlDbType.TinyInt).Value = domain.SignatureMethod;
        command.Parameters.Add("@SignaturePlainText", SqlDbType.NVarChar, -1).Value = domain.SignaturePlainText;
        command.Parameters.Add("@SignatureHtml", SqlDbType.NVarChar, -1).Value = domain.SignatureHtml;
        command.Parameters.Add("@AddSignaturesToReplies", SqlDbType.TinyInt).Value = domain.AddSignaturesToReplies ? 1 : 0;
        command.Parameters.Add("@AddSignaturesToLocalMail", SqlDbType.TinyInt).Value = domain.AddSignaturesToLocalMail ? 1 : 0;
        command.Parameters.Add("@MaxNumberOfAccounts", SqlDbType.Int).Value = domain.MaxNumberOfAccounts;
        command.Parameters.Add("@MaxNumberOfAliases", SqlDbType.Int).Value = domain.MaxNumberOfAliases;
        command.Parameters.Add("@MaxNumberOfDistributionLists", SqlDbType.Int).Value = domain.MaxNumberOfDistributionLists;
        command.Parameters.Add("@LimitationsEnabled", SqlDbType.TinyInt).Value =
            (domain.MaxNumberOfAccountsEnabled ? 1 : 0)
            | (domain.MaxNumberOfAliasesEnabled ? 2 : 0)
            | (domain.MaxNumberOfDistributionListsEnabled ? 4 : 0);
        command.Parameters.Add("@DkimSelector", SqlDbType.NVarChar, 255).Value = domain.DkimSelector;
        command.Parameters.Add("@DkimPrivateKeyFile", SqlDbType.NVarChar, 255).Value = domain.DkimPrivateKeyFile;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }
    public async ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(_connectionFactory, _transactionContext, GetDomainsSql, cancellationToken)
            .ConfigureAwait(false);
        var command = commandLease.Command;
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
            var adDomainName = reader.GetString(7);
            var allocatedSize = Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture);
            var sizeBytes = Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture);
            var size = Convert.ToInt32(sizeBytes / 1024 / 1024, CultureInfo.InvariantCulture);
            var maxSize = reader.GetInt32(10);
            var maxNumberOfAccounts = reader.GetInt32(11);
            var maxNumberOfAliases = reader.GetInt32(12);
            var maxNumberOfDistributionLists = reader.GetInt32(13);
            var limitationsEnabled = Convert.ToInt32(reader.GetValue(14), CultureInfo.InvariantCulture);
            var maxAccountSize = reader.GetInt32(15);
            var signatureEnabled = Convert.ToInt32(reader.GetValue(16), CultureInfo.InvariantCulture) != 0;
            var signatureMethod = Convert.ToInt32(reader.GetValue(17), CultureInfo.InvariantCulture);
            var signaturePlainText = reader.GetString(18);
            var signatureHtml = reader.GetString(19);
            var addSignaturesToReplies = Convert.ToInt32(reader.GetValue(20), CultureInfo.InvariantCulture) != 0;
            var addSignaturesToLocalMail = Convert.ToInt32(reader.GetValue(21), CultureInfo.InvariantCulture) != 0;
            var antiSpamOptions = Convert.ToInt32(reader.GetValue(22), CultureInfo.InvariantCulture);
            domains.Add(
                new DomainAdministrationSnapshot(
                    Id: id,
                    Name: name,
                    Active: active,
                    Postmaster: postmaster,
                    MaxMessageSize: maxMessageSize,
                    PlusAddressingEnabled: plusAddressingEnabled,
                    PlusAddressingCharacter: plusAddressingCharacter,
                    AntiSpamEnableGreylisting: (antiSpamOptions & AntiSpamOptionUseGreylisting) != 0,
                    AdDomainName: adDomainName,
                    MaxSize: maxSize,
                    Size: size,
                    AllocatedSize: allocatedSize,
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
                    DkimSelector: reader.GetString(23),
                    DkimPrivateKeyFile: reader.GetString(24),
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
