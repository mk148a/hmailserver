using System.Data;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpRecipientValidator : ISmtpRecipientValidator
{
    public const string SelectRoutesSql = """
SELECT
    routeid,
    routedomainname,
    routealladdresses,
    routetreatsecurityaslocal,
    routetargetsmthost,
    routetargetsmtport,
    routeconnectionsecurity,
    routeuseauthentication,
    routeauthenticationusername,
    routeauthenticationpassword
FROM hm_routes
ORDER BY routedomainname ASC;
""";

    public const string SelectRouteAddressSql = """
SELECT TOP (1) 1
FROM hm_routeaddresses
WHERE
    routeaddressrouteid = @RouteId
    AND LOWER(routeaddressaddress) = LOWER(@Address);
""";

    public const string SelectDomainSql = """
SELECT TOP (1)
    d.domainid,
    d.domainname,
    d.domainactive,
    d.domainpostmaster,
    d.domainuseplusaddressing,
    d.domainplusaddressingchar
FROM hm_domains AS d
WHERE LOWER(d.domainname) = LOWER(@DomainName);
""";

    public const string SelectDomainByAliasSql = """
SELECT TOP (1)
    d.domainid,
    d.domainname,
    d.domainactive,
    d.domainpostmaster,
    d.domainuseplusaddressing,
    d.domainplusaddressingchar
FROM hm_domain_aliases AS da
INNER JOIN hm_domains AS d
    ON d.domainid = da.dadomainid
WHERE LOWER(da.daalias) = LOWER(@DomainName);
""";

    public const string SelectAccountSql = """
SELECT TOP (1)
    accountid,
    accountaddress,
    accountactive
FROM hm_accounts
WHERE LOWER(accountaddress) = LOWER(@Address);
""";

    public const string SelectAliasSql = """
SELECT TOP (1)
    aliasvalue,
    aliasactive
FROM hm_aliases
WHERE LOWER(aliasname) = LOWER(@Address);
""";

    public const string SelectDistributionListSql = """
SELECT TOP (1)
    distributionlistid,
    distributionlistaddress,
    distributionlistenabled,
    distributionlistrequireauth,
    distributionlistmode,
    distributionlistrequireaddress
FROM hm_distributionlists
WHERE LOWER(distributionlistaddress) = LOWER(@Address);
""";

    public const string SelectDistributionListMembersSql = """
SELECT distributionlistrecipientaddress
FROM hm_distributionlistsrecipients
WHERE distributionlistrecipientlistid = @DistributionListId
ORDER BY distributionlistrecipientid ASC;
""";

    private const int MaxRecursionDepth = 25;

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerSmtpRecipientValidator(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<SmtpRecipientValidationResult> ValidateAsync(
        SmtpRecipientValidationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryNormalizeAddress(request.RecipientAddress, out var normalizedRecipient))
        {
            return SmtpRecipientValidationResult.Reject("501 Recipient address is invalid");
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var recipients = new List<SmtpResolvedRecipient>();
        var result = await ResolveRecipientAsync(
            connection,
            request,
            normalizedRecipient,
            originalAddress: request.RecipientAddress,
            recursionDepth: 0,
            recipients,
            cancellationToken).ConfigureAwait(false);

        if (!result.Accepted)
        {
            return result;
        }

        return recipients.Count == 0
            ? SmtpRecipientValidationResult.Reject("550 Unknown user")
            : new SmtpRecipientValidationResult(Accepted: true, recipients, FailureResponse: null);
    }

    private async ValueTask<SmtpRecipientValidationResult> ResolveRecipientAsync(
        SqlConnection connection,
        SmtpRecipientValidationRequest request,
        string recipientAddress,
        string originalAddress,
        int recursionDepth,
        List<SmtpResolvedRecipient> recipients,
        CancellationToken cancellationToken)
    {
        if (recursionDepth > MaxRecursionDepth)
        {
            return SmtpRecipientValidationResult.Reject("554 Mail server configuration error. Too many recursive forwards.");
        }

        if (!TrySplitAddress(recipientAddress, out var localPart, out var domainName))
        {
            return SmtpRecipientValidationResult.Reject("501 Recipient address is invalid");
        }

        var domain = await LoadDomainAsync(connection, domainName, cancellationToken).ConfigureAwait(false);
        if (domain is not null && !domain.Active)
        {
            return SmtpRecipientValidationResult.Reject("550 Domain has been disabled.");
        }

        if (domain is not null)
        {
            var primaryAddress = ApplyPlusAddressing(localPart, domain) + "@" + domain.Name;
            var account = await LoadAccountAsync(connection, primaryAddress, cancellationToken).ConfigureAwait(false);
            if (account is not null)
            {
                if (!account.Active)
                {
                    return SmtpRecipientValidationResult.Reject("550 Account is not active.");
                }

                AddRecipient(
                    recipients,
                    new SmtpResolvedRecipient(
                        account.Address,
                        originalAddress,
                        account.AccountId,
                        IsLocal: true));
                return SmtpRecipientValidationResult.Accept();
            }

            var alias = await LoadAliasAsync(connection, primaryAddress, cancellationToken).ConfigureAwait(false);
            if (alias is not null)
            {
                if (!alias.Active)
                {
                    return SmtpRecipientValidationResult.Reject("550 Alias is not active.");
                }

                return await ResolveRecipientAsync(
                    connection,
                    request,
                    alias.Value,
                    originalAddress,
                    recursionDepth + 1,
                    recipients,
                    cancellationToken).ConfigureAwait(false);
            }

            var distributionList = await LoadDistributionListAsync(connection, primaryAddress, cancellationToken).ConfigureAwait(false);
            if (distributionList is not null)
            {
                if (!distributionList.Active)
                {
                    return SmtpRecipientValidationResult.Reject("550 Distribution list is not active.");
                }

                var authorizationResult = await ValidateDistributionListAuthorizationAsync(
                    connection,
                    request,
                    distributionList,
                    domain,
                    cancellationToken).ConfigureAwait(false);
                if (!authorizationResult.Accepted)
                {
                    return authorizationResult;
                }

                var members = await LoadDistributionListMembersAsync(
                    connection,
                    distributionList.ListId,
                    cancellationToken).ConfigureAwait(false);
                foreach (var member in members)
                {
                    var memberResult = await ResolveRecipientAsync(
                        connection,
                        request,
                        member,
                        originalAddress,
                        recursionDepth + 1,
                        recipients,
                        cancellationToken).ConfigureAwait(false);
                    if (!memberResult.Accepted)
                    {
                        return memberResult;
                    }
                }

                return SmtpRecipientValidationResult.Accept();
            }
        }

        var route = await LoadRouteAsync(connection, domainName, cancellationToken).ConfigureAwait(false);
        if (route is not null)
        {
            if (route.ToAllAddresses ||
                await RouteContainsAddressAsync(connection, route.RouteId, recipientAddress, cancellationToken).ConfigureAwait(false))
            {
                AddRecipient(
                    recipients,
                    new SmtpResolvedRecipient(
                        recipientAddress,
                        originalAddress,
                        LocalAccountId: 0,
                        IsLocal: route.TreatRecipientAsLocal,
                        Route: route.ToResolution()));
                return SmtpRecipientValidationResult.Accept();
            }

            if (domain is null || string.IsNullOrWhiteSpace(domain.Postmaster))
            {
                return SmtpRecipientValidationResult.Reject("550 Recipient not in route list.");
            }
        }

        if (domain is null)
        {
            if (!request.SenderAuthenticated)
            {
                return SmtpRecipientValidationResult.Reject("550 Relay not permitted");
            }

            AddRecipient(
                recipients,
                new SmtpResolvedRecipient(
                    recipientAddress,
                    originalAddress,
                    LocalAccountId: 0,
                    IsLocal: false));
            return SmtpRecipientValidationResult.Accept();
        }

        if (!string.IsNullOrWhiteSpace(domain.Postmaster))
        {
            var postmaster = domain.Postmaster.Contains('@', StringComparison.Ordinal)
                ? domain.Postmaster
                : domain.Postmaster + "@" + domain.Name;
            return await ResolveRecipientAsync(
                connection,
                request,
                postmaster,
                originalAddress,
                recursionDepth + 1,
                recipients,
                cancellationToken).ConfigureAwait(false);
        }

        return SmtpRecipientValidationResult.Reject("550 Unknown user");
    }

    private async ValueTask<RouteInfo?> LoadRouteAsync(
        SqlConnection connection,
        string domainName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectRoutesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var route = new RouteInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                ToBoolean(reader.GetValue(2)),
                ToBoolean(reader.GetValue(3)),
                reader.GetString(4),
                Convert.ToInt32(reader.GetValue(5), System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(6), System.Globalization.CultureInfo.InvariantCulture),
                ToBoolean(reader.GetValue(7)),
                reader.GetString(8),
                DecryptRoutePassword(reader.GetString(9)));
            if (WildcardMatchNoCase(route.DomainName, domainName))
            {
                return route;
            }
        }

        return null;
    }

    private static async ValueTask<bool> RouteContainsAddressAsync(
        SqlConnection connection,
        int routeId,
        string address,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectRouteAddressSql, connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = address;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result != DBNull.Value;
    }

    private async ValueTask<SmtpRecipientValidationResult> ValidateDistributionListAuthorizationAsync(
        SqlConnection connection,
        SmtpRecipientValidationRequest request,
        DistributionListInfo distributionList,
        DomainInfo distributionListDomain,
        CancellationToken cancellationToken)
    {
        if (distributionList.RequireAuth && !request.SenderAuthenticated)
        {
            return SmtpRecipientValidationResult.Reject("550 SMTP authentication required.");
        }

        var normalizedSender = TryNormalizeAddress(request.MailFrom, out var sender)
            ? await ApplyDomainAliasAsync(connection, sender, cancellationToken).ConfigureAwait(false)
            : string.Empty;

        return distributionList.ListMode switch
        {
            DistributionListMode.Public => SmtpRecipientValidationResult.Accept(),
            DistributionListMode.Announcement when normalizedSender.Equals(
                await ApplyDomainAliasAsync(connection, distributionList.RequireAddress, cancellationToken).ConfigureAwait(false),
                StringComparison.OrdinalIgnoreCase) => SmtpRecipientValidationResult.Accept(),
            DistributionListMode.Announcement => SmtpRecipientValidationResult.Reject("550 Not authorized owner."),
            DistributionListMode.DomainMembers when TrySplitAddress(normalizedSender, out _, out var senderDomain) &&
                                                    senderDomain.Equals(distributionListDomain.Name, StringComparison.OrdinalIgnoreCase) =>
                SmtpRecipientValidationResult.Accept(),
            DistributionListMode.DomainMembers => SmtpRecipientValidationResult.Reject("550 Not authorized domain."),
            DistributionListMode.Membership when await IsDistributionListMemberAsync(
                connection,
                distributionList.ListId,
                normalizedSender,
                cancellationToken).ConfigureAwait(false) => SmtpRecipientValidationResult.Accept(),
            DistributionListMode.Membership => SmtpRecipientValidationResult.Reject("550 Not authorized sender."),
            _ => SmtpRecipientValidationResult.Reject("550 Not authorized sender.")
        };
    }

    private async ValueTask<string> ApplyDomainAliasAsync(
        SqlConnection connection,
        string address,
        CancellationToken cancellationToken)
    {
        if (!TrySplitAddress(address, out var localPart, out var domainName))
        {
            return address;
        }

        var domain = await LoadDomainAsync(connection, domainName, cancellationToken).ConfigureAwait(false);
        return domain is null ? address : localPart + "@" + domain.Name;
    }

    private async ValueTask<bool> IsDistributionListMemberAsync(
        SqlConnection connection,
        int listId,
        string sender,
        CancellationToken cancellationToken)
    {
        var members = await LoadDistributionListMembersAsync(connection, listId, cancellationToken).ConfigureAwait(false);
        foreach (var member in members)
        {
            var normalizedMember = await ApplyDomainAliasAsync(connection, member, cancellationToken).ConfigureAwait(false);
            if (normalizedMember.Equals(sender, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<DomainInfo?> LoadDomainAsync(
        SqlConnection connection,
        string domainName,
        CancellationToken cancellationToken)
    {
        var direct = await LoadDomainCoreAsync(connection, SelectDomainSql, domainName, cancellationToken).ConfigureAwait(false);
        return direct ?? await LoadDomainCoreAsync(connection, SelectDomainByAliasSql, domainName, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<DomainInfo?> LoadDomainCoreAsync(
        SqlConnection connection,
        string sql,
        string domainName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@DomainName", SqlDbType.NVarChar, 255).Value = domainName;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new DomainInfo(
            reader.GetInt32(0),
            reader.GetString(1),
            ToBoolean(reader.GetValue(2)),
            reader.GetString(3),
            ToBoolean(reader.GetValue(4)),
            reader.GetString(5));
    }

    private static async ValueTask<AccountInfo?> LoadAccountAsync(
        SqlConnection connection,
        string address,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectAccountSql, connection);
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = address;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new AccountInfo(reader.GetInt32(0), reader.GetString(1), ToBoolean(reader.GetValue(2)));
    }

    private static async ValueTask<AliasInfo?> LoadAliasAsync(
        SqlConnection connection,
        string address,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectAliasSql, connection);
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = address;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new AliasInfo(reader.GetString(0), ToBoolean(reader.GetValue(1)));
    }

    private static async ValueTask<DistributionListInfo?> LoadDistributionListAsync(
        SqlConnection connection,
        string address,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectDistributionListSql, connection);
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = address;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new DistributionListInfo(
            reader.GetInt32(0),
            reader.GetString(1),
            ToBoolean(reader.GetValue(2)),
            ToBoolean(reader.GetValue(3)),
            (DistributionListMode)Convert.ToInt32(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
            reader.GetString(5));
    }

    private static async ValueTask<IReadOnlyList<string>> LoadDistributionListMembersAsync(
        SqlConnection connection,
        int listId,
        CancellationToken cancellationToken)
    {
        var members = new List<string>();
        await using var command = new SqlCommand(SelectDistributionListMembersSql, connection);
        command.Parameters.Add("@DistributionListId", SqlDbType.Int).Value = listId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            members.Add(reader.GetString(0));
        }

        return members;
    }

    private static string ApplyPlusAddressing(
        string localPart,
        DomainInfo domain)
    {
        if (!domain.UsePlusAddressing || string.IsNullOrEmpty(domain.PlusAddressingCharacter))
        {
            return localPart;
        }

        var index = localPart.IndexOf(domain.PlusAddressingCharacter, StringComparison.Ordinal);
        return index > 0 ? localPart[..index] : localPart;
    }

    private static void AddRecipient(
        List<SmtpResolvedRecipient> recipients,
        SmtpResolvedRecipient recipient)
    {
        if (recipients.Any(existing =>
                existing.Address.Equals(recipient.Address, StringComparison.OrdinalIgnoreCase) &&
                existing.LocalAccountId == recipient.LocalAccountId))
        {
            return;
        }

        recipients.Add(recipient);
    }

    private static bool TryNormalizeAddress(
        string address,
        out string normalizedAddress)
    {
        normalizedAddress = address.Trim();
        return TrySplitAddress(normalizedAddress, out var localPart, out var domainName) &&
               localPart.Length > 0 &&
               domainName.Length > 0;
    }

    private static bool TrySplitAddress(
        string address,
        out string localPart,
        out string domainName)
    {
        localPart = string.Empty;
        domainName = string.Empty;

        var trimmed = address.Trim();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at >= trimmed.Length - 1)
        {
            return false;
        }

        localPart = trimmed[..at];
        domainName = trimmed[(at + 1)..];
        return true;
    }

    private static bool ToBoolean(object value) =>
        Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;

    private static string DecryptRoutePassword(string encryptedPassword) =>
        LegacyBlowfishPasswordCipher.TryDecrypt(encryptedPassword, out var decrypted)
            ? decrypted
            : string.Empty;

    private static bool WildcardMatchNoCase(
        string pattern,
        string value)
    {
        return WildcardMatch(pattern.AsSpan(), value.AsSpan());

        static bool WildcardMatch(
            ReadOnlySpan<char> pattern,
            ReadOnlySpan<char> value)
        {
            var patternIndex = 0;
            var valueIndex = 0;
            var starIndex = -1;
            var matchIndex = 0;

            while (valueIndex < value.Length)
            {
                if (patternIndex < pattern.Length &&
                    (pattern[patternIndex] == '?' ||
                     char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
                {
                    patternIndex++;
                    valueIndex++;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex;
                    matchIndex = valueIndex;
                    patternIndex++;
                }
                else if (starIndex != -1)
                {
                    patternIndex = starIndex + 1;
                    matchIndex++;
                    valueIndex = matchIndex;
                }
                else
                {
                    return false;
                }
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }
    }

    private sealed record DomainInfo(
        int DomainId,
        string Name,
        bool Active,
        string Postmaster,
        bool UsePlusAddressing,
        string PlusAddressingCharacter);

    private sealed record AccountInfo(
        int AccountId,
        string Address,
        bool Active);

    private sealed record AliasInfo(
        string Value,
        bool Active);

    private sealed record DistributionListInfo(
        int ListId,
        string Address,
        bool Active,
        bool RequireAuth,
        DistributionListMode ListMode,
        string RequireAddress);

    private sealed record RouteInfo(
        int RouteId,
        string DomainName,
        bool ToAllAddresses,
        bool TreatRecipientAsLocal,
        string TargetHost,
        int TargetPort,
        int ConnectionSecurity,
        bool RequiresAuthentication,
        string AuthenticationUsername,
        string AuthenticationPassword)
    {
        public SmtpRouteResolution ToResolution() =>
            new(
                RouteId,
                DomainName,
                TargetHost,
                TargetPort,
                ConnectionSecurity,
                TreatRecipientAsLocal,
                RequiresAuthentication,
                AuthenticationUsername,
                AuthenticationPassword);
    }

    private enum DistributionListMode
    {
        Public = 0,
        Membership = 1,
        Announcement = 2,
        DomainMembers = 3
    }
}
