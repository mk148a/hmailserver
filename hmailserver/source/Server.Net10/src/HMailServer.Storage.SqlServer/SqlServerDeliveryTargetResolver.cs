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

    public const string SelectVerifyRemoteSslCertificateSql = """
SELECT settinginteger
FROM hm_settings
WHERE settingname = N'VerifyRemoteSslCertificate';
""";

    public const string SelectSmtpRelayerSql = """
SELECT
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayer' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'usesmtprelayerauthentication' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayerusername' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayerport' THEN settinginteger END), 0),
COALESCE(MAX(CASE WHEN settingname = N'smtprelayerconnectionsecurity' THEN settinginteger END), 0),
COALESCE(MAX(CASE WHEN settingname = N'smtprelayerpassword' THEN settingstring END), N'')
FROM hm_settings
WHERE settingname IN (
    N'smtprelayer', N'usesmtprelayerauthentication', N'smtprelayerusername',
    N'smtprelayerport', N'smtprelayerconnectionsecurity', N'smtprelayerpassword');
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
        bool? verifyRemoteSslCertificate = null;
        RelayerInfo? smtpRelayer = null;
        var smtpRelayerLoaded = false;
        var groups = new Dictionary<string, TargetGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in message.Recipients)
        {
            DeliveryTarget target;
            if (recipient.LocalAccountId > 0)
            {
                target = CreateLocalTarget(recipient);
            }
            else if (forcedRoute is not null)
            {
                verifyRemoteSslCertificate ??= await LoadVerifyRemoteSslCertificateAsync(
                    connection,
                    cancellationToken).ConfigureAwait(false);
                target = CreateForcedRouteTarget(recipient, forcedRoute, verifyRemoteSslCertificate.Value);
            }
            else
            {
                target = await CreateRemoteOrRouteTargetAsync(
                    connection,
                    recipient,
                    async () => remoteConnectionSecurity ??= await LoadSmtpConnectionSecurityAsync(connection, cancellationToken).ConfigureAwait(false),
                    async () => verifyRemoteSslCertificate ??= await LoadVerifyRemoteSslCertificateAsync(connection, cancellationToken).ConfigureAwait(false),
                    async () =>
                    {
                        if (!smtpRelayerLoaded)
                        {
                            smtpRelayer = await LoadSmtpRelayerAsync(connection, cancellationToken).ConfigureAwait(false);
                            smtpRelayerLoaded = true;
                        }

                        return smtpRelayer;
                    },
                    cancellationToken).ConfigureAwait(false);
            }
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
        RouteInfo route,
        bool verifyRemoteSslCertificate)
    {
        var domainName = TrySplitAddress(recipient.Address, out _, out var domain)
            ? domain
            : route.DomainName;
        var resolution = route.ToResolution();
        return new DeliveryTarget(
            DeliveryTargetKind.Route,
            Key: "route:" + route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DomainName: domainName,
            Route: resolution,
            VerifyRemoteSslCertificate: verifyRemoteSslCertificate);
    }

    private static async ValueTask<DeliveryTarget> CreateRemoteOrRouteTargetAsync(
        SqlConnection connection,
        DeliveryQueueRecipient recipient,
        Func<ValueTask<int>> loadRemoteConnectionSecurityAsync,
        Func<ValueTask<bool>> loadVerifyRemoteSslCertificateAsync,
        Func<ValueTask<RelayerInfo?>> loadSmtpRelayerAsync,
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
                Route: resolution,
                VerifyRemoteSslCertificate: await loadVerifyRemoteSslCertificateAsync().ConfigureAwait(false));
        }

        var smtpRelayer = await loadSmtpRelayerAsync().ConfigureAwait(false);
        if (smtpRelayer is not null)
        {
            var resolution = smtpRelayer.ToResolution(domainName);
            return new DeliveryTarget(
                DeliveryTargetKind.Route,
                Key: "relayer:" + smtpRelayer.Host,
                DomainName: domainName,
                Route: resolution,
                VerifyRemoteSslCertificate: await loadVerifyRemoteSslCertificateAsync().ConfigureAwait(false));
        }

        return new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            Key: "remote:" + domainName,
            DomainName: domainName,
            RemoteConnectionSecurity: await loadRemoteConnectionSecurityAsync().ConfigureAwait(false),
            VerifyRemoteSslCertificate: await loadVerifyRemoteSslCertificateAsync().ConfigureAwait(false));
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

    private static async ValueTask<bool> LoadVerifyRemoteSslCertificateAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectVerifyRemoteSslCertificateSql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null
            or DBNull
            || Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;
    }

    private static async ValueTask<RelayerInfo?> LoadSmtpRelayerAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectSmtpRelayerSql, connection);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var host = reader.GetString(0).Trim();
        if (host.Length == 0)
        {
            return null;
        }

        var hosts = host
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0)
        {
            return null;
        }

        host = string.Join('|', hosts);

        var requiresAuthentication = ToBoolean(reader.GetValue(1));
        var username = reader.GetString(2);
        var encryptedPassword = reader.GetString(5);
        var password = string.Empty;
        var shouldAuthenticate = requiresAuthentication && username.Length != 0;
        var connectionSecurity = Convert.ToInt32(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture);
        if (connectionSecurity is < 0 or > 3)
        {
            throw new InvalidOperationException(
                $"Global SMTP relayer connection security value {connectionSecurity} is invalid.");
        }

        if (shouldAuthenticate && encryptedPassword.Length != 0 && !LegacyBlowfishPasswordCipher.TryDecrypt(encryptedPassword, out password))
        {
            throw new InvalidOperationException("The configured SMTP relayer password could not be decrypted.");
        }

        return new RelayerInfo(
            host,
            shouldAuthenticate,
            username,
            Convert.ToInt32(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture),
            connectionSecurity,
            password);
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

    private static RouteInfo ReadRouteInfo(SqlDataReader reader)
    {
        var requiresAuthentication = ToBoolean(reader.GetValue(6));
        return new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            Convert.ToInt32(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
            ToBoolean(reader.GetValue(5)),
            requiresAuthentication,
            reader.GetString(7),
            DecryptRoutePassword(reader.GetString(8)));
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

    private sealed record RelayerInfo(
        string Host,
        bool RequiresAuthentication,
        string Username,
        int Port,
        int ConnectionSecurity,
        string Password)
    {
        public SmtpRouteResolution ToResolution(string domainName) =>
            new(
                RouteId: 0,
                DomainName: domainName,
                TargetHost: Host,
                TargetPort: Port,
                ConnectionSecurity,
                TreatRecipientAsLocal: false,
                RequiresAuthentication,
                Username,
                Password);
    }
}
