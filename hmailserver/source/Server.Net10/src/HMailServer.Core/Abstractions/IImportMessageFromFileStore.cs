namespace HMailServer.Core.Abstractions;

public interface IImportMessageFromFileStore
{
    ValueTask<ImportedMessageReference?> FindExistingMessageAsync(
        string? partialFileName,
        string fullFileName,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMessageFileNameAsync(
        long messageId,
        string partialFileName,
        CancellationToken cancellationToken);

    ValueTask ImportDeliveredMessageAsync(
        ImportedDeliveredMessage message,
        CancellationToken cancellationToken);

    ValueTask ImportQueuedMessageAsync(
        ImportedQueuedMessage message,
        CancellationToken cancellationToken);
}

public sealed record ImportedMessageReference(
    long MessageId,
    bool IsPartialFileName);

public sealed record ImportedDeliveredMessage(
    int AccountId,
    string FileName,
    string FromAddress,
    long Size,
    DateTimeOffset CreatedUtc);

public sealed record ImportedQueuedMessage(
    string FileName,
    string FromAddress,
    long Size,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<SmtpResolvedRecipient> Recipients);
