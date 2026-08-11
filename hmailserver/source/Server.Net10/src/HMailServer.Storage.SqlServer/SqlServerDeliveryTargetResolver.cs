using System.Data;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryTargetResolver : IDeliveryTargetResolver
{
    public const string SelectSmtpConnectionSecuritySql = """
SELECT settinginteger
FROM hm_settings
WHERE settingname = N'SmtpDeliveryConnectionSecurity';
""";

    public const string SelectRoutesSql = """
SELECT
    routeid,
    routedomainname,
    routetargetsmthost,
    routetargetsmtport,
    routeconnectionsecurity,
    routetreatsecurityaslocal,
    routeuseauthentication,
    routeauthenticationusername,
    routeauthenticationpassword
FROM hm_routes
ORDER BY routedomainname ASC;
""";

    public const string SelectRouteByIdSql = """
SELECT
    routeid,
    routedomainname,
    routetargetsmthost,
    routetargetsmtport,
    routeconnectionsecurity,
    routetreatsecurityaslocal,
    routeuseauthentication,
    routeauthenticationusername,
    routeauthenticationpassword
FROM hm_routes
WHERE routeid = @RouteId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryTargetResolver(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DeliveryTargetBatch>> ResolveAsync(
        DeliveryQueuedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var forcedRoute = message.RuleForcedRouteId > 0
            ? await LoadRouteByIdAsync(connection, message.RuleForcedRouteId, cancellationToken).ConfigureAwait(false)
            : null;
        int? remoteConnectionSecurity = null;
        var groups = new Dictionary<string, TargetGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in message.Recipients)
        {
            var target = recipient.LocalAccountId > 0
                    ? CreateLocalTarget(recipient)
                    : forcedRoute is not null
                        ? CreateForcedRouteTarget(recipient, forcedRoute)
                    : await CreateRemoteOrRouteTargetAsync(
                        connection,
                        recipient,
                        async () => remoteConnectionSecurity ??= await LoadSmtpConnectionSecurityAsync(connection, cancellationToken).ConfigureAwait(false),
                        cancellationToken).ConfigureAwait(false);
            var groupKey = target.Key;
            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new TargetGroup(target);
                groups.Add(groupKey, group);
            }

            group.Recipients.Add(recipient);
        }

        return groups.Values
            .Select(group => new DeliveryTargetBatch(group.Target, group.Recipients))
            .ToArray();
    }

    private static DeliveryTarget CreateLocalTarget(DeliveryQueueRecipient recipient)
    {
        var domainName = TrySplitAddress(recipient.Address, out _, out var domain)
            ? domain
            : string.Empty;
        return new DeliveryTarget(
            DeliveryTargetKind.LocalAccount,
            Key: "local:" + recipient.LocalAccountId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DomainName: domainName,
            LocalAccountId: recipient.LocalAccountId);
    }

    private static DeliveryTarget CreateForcedRouteTarget(
        DeliveryQueueRecipient recipient,
        RouteInfo route)
    {
        var domainName = TrySplitAddress(recipient.Address, out _, out var domain)
            ? domain
            : route.DomainName;
        var resolution = route.ToResolution();
        return new DeliveryTarget(
            DeliveryTargetKind.Route,
            Key: "route:" + route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DomainName: domainName,
            Route: resolution);
    }

    private static async ValueTask<DeliveryTarget> CreateRemoteOrRouteTargetAsync(
        SqlConnection connection,
        DeliveryQueueRecipient recipient,
        Func<ValueTask<int>> loadRemoteConnectionSecurityAsync,
        CancellationToken cancellationToken)
    {
        var domainName = TrySplitAddress(recipient.Address, out _, out var domain)
            ? domain
            : string.Empty;
        var route = string.IsNullOrWhiteSpace(domainName)
            ? null
            : await LoadRouteAsync(connection, domainName, cancellationToken).ConfigureAwait(false);
        if (route is not null)
        {
            var resolution = route.ToResolution();
            return new DeliveryTarget(
                DeliveryTargetKind.Route,
                Key: "route:" + route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DomainName: domainName,
                Route: resolution);
        }

        return new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            Key: "remote:" + domainName,
            DomainName: domainName,
            RemoteConnectionSecurity: await loadRemoteConnectionSecurityAsync().ConfigureAwait(false));
    }

    private static async ValueTask<int> LoadSmtpConnectionSecurityAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectSmtpConnectionSecuritySql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull
            ? 0
            : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask<RouteInfo?> LoadRouteAsync(
        SqlConnection connection,
        string domainName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectRoutesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var route = ReadRouteInfo(reader);
            if (WildcardMatchNoCase(route.DomainName, domainName))
            {
                return route;
            }
        }

        return null;
    }

    private static async ValueTask<RouteInfo?> LoadRouteByIdAsync(
        SqlConnection connection,
        int routeId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectRouteByIdSql, connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadRouteInfo(reader);
    }

    private static RouteInfo ReadRouteInfo(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            Convert.ToInt32(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
            ToBoolean(reader.GetValue(5)),
            ToBoolean(reader.GetValue(6)),
            reader.GetString(7),
            DecryptRoutePassword(reader.GetString(8)));

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

    private static bool WildcardMatchNoCase(
        string pattern,
        string value)
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

    private static bool ToBoolean(object value) =>
        Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;

    private static string DecryptRoutePassword(string encryptedPassword) =>
        LegacyBlowfishPasswordCipher.TryDecrypt(encryptedPassword, out var decrypted)
            ? decrypted
            : string.Empty;

    private sealed class TargetGroup
    {
        public TargetGroup(DeliveryTarget target)
        {
            Target = target;
        }

        public DeliveryTarget Target { get; }

        public List<DeliveryQueueRecipient> Recipients { get; } = [];
    }

    private sealed record RouteInfo(
        int RouteId,
        string DomainName,
        string TargetHost,
        int TargetPort,
        int ConnectionSecurity,
        bool TreatRecipientAsLocal,
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
}
