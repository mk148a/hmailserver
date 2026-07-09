using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSslCertificateAdministrationStore : ISslCertificateAdministrationStore
{
    public const string GetSslCertificatesSql = """
SELECT
    sslcertificateid,
    sslcertificatename,
    sslcertificatefile,
    sslprivatekeyfile
FROM hm_sslcertificates
ORDER BY sslcertificatename ASC;
""";

    public const string ClearSslCertificatesSql = """
DELETE FROM hm_sslcertificates;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerSslCertificateAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<SslCertificateAdministrationSnapshot>> GetSslCertificatesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetSslCertificatesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var certificates = new List<SslCertificateAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            certificates.Add(
                new SslCertificateAdministrationSnapshot(
                    Id: Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Name: reader.GetString(1),
                    CertificateFile: reader.GetString(2),
                    PrivateKeyFile: reader.GetString(3)));
        }

        return certificates;
    }

    public async ValueTask ClearSslCertificatesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ClearSslCertificatesSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
