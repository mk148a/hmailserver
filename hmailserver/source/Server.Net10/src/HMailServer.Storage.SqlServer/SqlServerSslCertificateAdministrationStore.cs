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

    public const string DeleteSslCertificateByIdSql = """
DELETE FROM hm_sslcertificates
WHERE sslcertificateid = @id;
""";

    public const string UpdateSslCertificateSql = """
UPDATE hm_sslcertificates
SET sslcertificatename = @name,
    sslcertificatefile = @certificateFile,
    sslprivatekeyfile = @privateKeyFile
WHERE sslcertificateid = @id;
""";

    public const string InsertSslCertificateSql = """
INSERT INTO hm_sslcertificates
    (sslcertificatename, sslcertificatefile, sslprivatekeyfile)
OUTPUT INSERTED.sslcertificateid
VALUES (@name, @certificateFile, @privateKeyFile);
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

    public async ValueTask DeleteSslCertificateByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteSslCertificateByIdSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpdateSslCertificateAsync(
        SslCertificateAdministrationSnapshot certificate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateSslCertificateSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = certificate.Id;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = certificate.Name;
        command.Parameters.Add("@certificateFile", SqlDbType.NVarChar, 255).Value = certificate.CertificateFile;
        command.Parameters.Add("@privateKeyFile", SqlDbType.NVarChar, 255).Value = certificate.PrivateKeyFile;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> InsertSslCertificateAsync(
        SslCertificateAdministrationSnapshot certificate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertSslCertificateSql, connection);
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = certificate.Name;
        command.Parameters.Add("@certificateFile", SqlDbType.NVarChar, 255).Value = certificate.CertificateFile;
        command.Parameters.Add("@privateKeyFile", SqlDbType.NVarChar, 255).Value = certificate.PrivateKeyFile;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }
}
