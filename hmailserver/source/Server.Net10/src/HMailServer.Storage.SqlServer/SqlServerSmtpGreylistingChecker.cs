using System.Data;
using System.Globalization;
using System.Net;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpGreylistingChecker : ISmtpGreylistingChecker
{
    public const string SelectWhiteAddressSql = """
SELECT TOP (1) 1
FROM hm_greylisting_whiteaddresses
WHERE @ClientIPAddress LIKE whiteipaddress ESCAPE N'/';
""";

    public const string SelectTripletSql = """
SELECT TOP (1)
    glid,
    glblockendtime
FROM hm_greylisting_triplets WITH (UPDLOCK, HOLDLOCK)
WHERE glipaddress1 = @IpAddress1
  AND ((@IpAddress2 IS NULL AND glipaddress2 IS NULL) OR glipaddress2 = @IpAddress2)
  AND glsenderaddress = @SenderAddress
  AND glrecipientaddress = @RecipientAddress;
""";

    public const string InsertTripletSql = """
INSERT INTO hm_greylisting_triplets
(
    glcreatetime,
    glblockendtime,
    gldeletetime,
    glipaddress1,
    glipaddress2,
    glsenderaddress,
    glrecipientaddress,
    glblockedcount,
    glpassedcount
)
VALUES
(
    @CreateTime,
    @BlockEndTime,
    @DeleteTime,
    @IpAddress1,
    @IpAddress2,
    @SenderAddress,
    @RecipientAddress,
    0,
    0
);
""";

    public const string MarkTripletBlockedSql = """
UPDATE hm_greylisting_triplets
SET glblockedcount = glblockedcount + 1
WHERE glid = @TripletId;
""";

    public const string MarkTripletPassedSql = """
UPDATE hm_greylisting_triplets
SET gldeletetime = @DeleteTime,
    glpassedcount = glpassedcount + 1
WHERE glid = @TripletId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SmtpGreylistingOptions _options;
    private readonly TimeProvider _timeProvider;

    public SqlServerSmtpGreylistingChecker(
        SqlServerConnectionFactory connectionFactory,
        SmtpGreylistingOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _connectionFactory = connectionFactory;
        _options = options ?? new SmtpGreylistingOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<SmtpGreylistingResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled
            || (_options.SkipAuthenticated && request.IsAuthenticated)
            || request.Recipients.Count == 0
            || !IPAddress.TryParse(request.ClientIPAddress, out var clientAddress))
        {
            return SmtpGreylistingResult.Passed;
        }

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (await IsWhiteListedAsync(connection, request.ClientIPAddress, cancellationToken).ConfigureAwait(false))
            {
                return SmtpGreylistingResult.Passed;
            }

            var ipParts = SqlServerIpAddressParts.From(clientAddress);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            SmtpGreylistingResult? firstDeferred = null;

            await using var transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
            foreach (var recipient in request.Recipients)
            {
                var recipientAddress = Truncate(recipient.Address, 200);
                var result = await CheckRecipientAsync(
                    connection,
                    transaction,
                    ipParts,
                    Truncate(request.MailFrom, 200),
                    recipientAddress,
                    now,
                    cancellationToken).ConfigureAwait(false);
                if (result.Deferred && firstDeferred is null)
                {
                    firstDeferred = result;
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return firstDeferred ?? SmtpGreylistingResult.Passed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SmtpGreylistingResult.Passed;
        }
    }

    private async ValueTask<SmtpGreylistingResult> CheckRecipientAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        string senderAddress,
        string recipientAddress,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var triplet = await FindTripletAsync(
            connection,
            transaction,
            ipParts,
            senderAddress,
            recipientAddress,
            cancellationToken).ConfigureAwait(false);
        if (triplet is null)
        {
            await InsertTripletAsync(
                connection,
                transaction,
                ipParts,
                senderAddress,
                recipientAddress,
                now,
                cancellationToken).ConfigureAwait(false);
            return SmtpGreylistingResult.Defer(recipientAddress, NormalizeFailureResponse(_options.FailureResponse));
        }

        if (now > triplet.BlockEndTime)
        {
            await MarkTripletPassedAsync(
                connection,
                transaction,
                triplet.TripletId,
                now + _options.PassedRecordLifetime,
                cancellationToken).ConfigureAwait(false);
            return SmtpGreylistingResult.Passed;
        }

        await MarkTripletBlockedAsync(connection, transaction, triplet.TripletId, cancellationToken)
            .ConfigureAwait(false);
        return SmtpGreylistingResult.Defer(recipientAddress, NormalizeFailureResponse(_options.FailureResponse));
    }

    private static async ValueTask<bool> IsWhiteListedAsync(
        SqlConnection connection,
        string clientIPAddress,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectWhiteAddressSql, connection);
        command.Parameters.Add("@ClientIPAddress", SqlDbType.NVarChar, 255).Value = clientIPAddress;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static async ValueTask<GreylistingTriplet?> FindTripletAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        string senderAddress,
        string recipientAddress,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectTripletSql, connection, transaction);
        AddTripletParameters(command, ipParts, senderAddress, recipientAddress);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
            .ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new GreylistingTriplet(
            reader.GetInt64(0),
            reader.GetDateTime(1));
    }

    private async ValueTask InsertTripletAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        string senderAddress,
        string recipientAddress,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertTripletSql, connection, transaction);
        AddTripletParameters(command, ipParts, senderAddress, recipientAddress);
        command.Parameters.Add("@CreateTime", SqlDbType.DateTime).Value = now;
        command.Parameters.Add("@BlockEndTime", SqlDbType.DateTime).Value = now + _options.InitialDelay;
        command.Parameters.Add("@DeleteTime", SqlDbType.DateTime).Value = now + _options.InitialRecordLifetime;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask MarkTripletBlockedAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long tripletId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(MarkTripletBlockedSql, connection, transaction);
        command.Parameters.Add("@TripletId", SqlDbType.BigInt).Value = tripletId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask MarkTripletPassedAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long tripletId,
        DateTime deleteTime,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(MarkTripletPassedSql, connection, transaction);
        command.Parameters.Add("@TripletId", SqlDbType.BigInt).Value = tripletId;
        command.Parameters.Add("@DeleteTime", SqlDbType.DateTime).Value = deleteTime;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddTripletParameters(
        SqlCommand command,
        SqlServerIpAddressParts ipParts,
        string senderAddress,
        string recipientAddress)
    {
        command.Parameters.Add("@IpAddress1", SqlDbType.BigInt).Value = ipParts.Address1;
        command.Parameters.Add("@IpAddress2", SqlDbType.BigInt).Value =
            ipParts.Address2 is { } address2 ? address2 : DBNull.Value;
        command.Parameters.Add("@SenderAddress", SqlDbType.NVarChar, 200).Value = senderAddress;
        command.Parameters.Add("@RecipientAddress", SqlDbType.NVarChar, 200).Value = recipientAddress;
    }

    private static string NormalizeFailureResponse(string response)
    {
        response = response.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (response.Length == 0)
        {
            return "451 Please try again later.";
        }

        return StartsWithSmtpReplyCode(response)
            ? response
            : "451 " + response;
    }

    private static string Truncate(string value, int maxLength)
    {
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static bool StartsWithSmtpReplyCode(string value) =>
        value.Length >= 4
        && char.IsDigit(value[0])
        && char.IsDigit(value[1])
        && char.IsDigit(value[2])
        && value[3] == ' ';

    private sealed record GreylistingTriplet(
        long TripletId,
        DateTime BlockEndTime);

    private sealed record SqlServerIpAddressParts(long Address1, long? Address2)
    {
        public static SqlServerIpAddressParts From(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return new SqlServerIpAddressParts(
                    ((long)bytes[0] << 24) |
                    ((long)bytes[1] << 16) |
                    ((long)bytes[2] << 8) |
                    bytes[3],
                    null);
            }

            if (bytes.Length != 16)
            {
                throw new NotSupportedException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Unsupported IP address length {0}.",
                    bytes.Length));
            }

            return new SqlServerIpAddressParts(
                ToSignedInt64(bytes.AsSpan(0, 8)),
                ToSignedInt64(bytes.AsSpan(8, 8)));
        }

        private static long ToSignedInt64(ReadOnlySpan<byte> bytes)
        {
            ulong value = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                value = (value << 8) | bytes[i];
            }

            return unchecked((long)value);
        }
    }
}
