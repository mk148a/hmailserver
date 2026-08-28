using System.Data;
using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;
using MimeKit;

namespace HMailServer.Storage.SqlServer;

public sealed class MessageFileSearchDocumentSource : IMessageSearchDocumentBatchSource
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
    m.messageid IN ({0})
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
        await foreach (var document in TryLoadBatchAsync([identity], cancellationToken).ConfigureAwait(false))
        {
            return document;
        }

        throw new InvalidOperationException("Single-item message document batch returned no result.");
    }

    public async IAsyncEnumerable<MessageSearchDocument?> TryLoadBatchAsync(
        IReadOnlyList<MessageIdentity> identities,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identities);
        cancellationToken.ThrowIfCancellationRequested();

        var metadataByMessageId = await LoadMetadataBatchAsync(identities, cancellationToken).ConfigureAwait(false);
        foreach (var identity in identities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!metadataByMessageId.TryGetValue(identity.MessageId, out var metadata))
            {
                yield return null;
                continue;
            }

            yield return await LoadDocumentAsync(metadata, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<MessageSearchDocument?> LoadDocumentAsync(
        MessageMetadata metadata,
        CancellationToken cancellationToken)
    {
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

    private async ValueTask<IReadOnlyDictionary<long, MessageMetadata>> LoadMetadataBatchAsync(
        IReadOnlyList<MessageIdentity> identities,
        CancellationToken cancellationToken)
    {
        var metadataByMessageId = new Dictionary<long, MessageMetadata>(identities.Count);
        if (identities.Count == 0)
        {
            return metadataByMessageId;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var parameterNames = new string[identities.Count];
        for (var index = 0; index < identities.Count; index++)
        {
            var parameterName = $"@MessageId{index}";
            parameterNames[index] = parameterName;
        }

        await using var command = new SqlCommand(
            string.Format(MetadataSql, string.Join(", ", parameterNames)),
            connection);
        for (var index = 0; index < identities.Count; index++)
        {
            command.Parameters.Add(parameterNames[index], SqlDbType.BigInt).Value = identities[index].MessageId;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var identity = new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3));

            var createTime = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
            metadataByMessageId[identity.MessageId] = new MessageMetadata(
                identity,
                reader.GetString(4),
                new DateTimeOffset(createTime),
                reader.GetInt64(6),
                reader.GetByte(7),
                reader.IsDBNull(8) ? null : reader.GetString(8));
        }

        return metadataByMessageId;
    }

    private sealed record MessageMetadata(
        MessageIdentity Identity,
        string MessageFileName,
        DateTimeOffset InternalDateUtc,
        long SizeBytes,
        byte Flags,
        string? AccountAddress);
}
