using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;
using MimeKit;

namespace HMailServer.Storage.SqlServer;

public sealed class MessageFileSearchDocumentSource : IMessageSearchDocumentSource
{
    private const string MetadataSql = """
SELECT
    m.messageid,
    m.messageaccountid,
    m.messagefolderid,
    m.messageuid,
    m.messagefilename,
    m.messagecreatetime,
    m.messagesize,
    m.messageflags,
    a.accountaddress
FROM hm_messages AS m
LEFT JOIN hm_accounts AS a
    ON a.accountid = m.messageaccountid
WHERE
    m.messageid = @MessageId
    AND m.messagetype = 2;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;
    private readonly MessageFileSearchDocumentSourceOptions _options;

    public MessageFileSearchDocumentSource(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        MessageFileSearchDocumentSourceOptions options)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
        _options = options;
    }

    public async ValueTask<MessageSearchDocument?> TryLoadAsync(
        MessageIdentity identity,
        CancellationToken cancellationToken)
    {
        var metadata = await LoadMetadataAsync(identity.MessageId, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var filePath = _pathResolver.Resolve(
            metadata.MessageFileName,
            metadata.Identity.AccountId,
            metadata.Identity.FolderId,
            metadata.AccountAddress);

        if (filePath is null || !File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var message = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        var text = MimeMessageSearchTextExtractor.Extract(message, _options);

        return new MessageSearchDocument(
            metadata.Identity,
            metadata.InternalDateUtc,
            metadata.SizeBytes,
            metadata.Flags,
            text.HeaderText,
            text.BodyText,
            text.CombinedText)
        {
            SubjectText = text.SubjectText,
            FileSearchHeaderText = text.FileSearchHeaderText,
            FileSearchPlainBodyText = text.FileSearchPlainBodyText,
            FileSearchHtmlBodyText = text.FileSearchHtmlBodyText
        };
    }

    private async ValueTask<MessageMetadata?> LoadMetadataAsync(
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(MetadataSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var identity = new MessageIdentity(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt64(3));

        var createTime = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);

        return new MessageMetadata(
            identity,
            reader.GetString(4),
            new DateTimeOffset(createTime),
            reader.GetInt64(6),
            reader.GetByte(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    private sealed record MessageMetadata(
        MessageIdentity Identity,
        string MessageFileName,
        DateTimeOffset InternalDateUtc,
        long SizeBytes,
        byte Flags,
        string? AccountAddress);
}
