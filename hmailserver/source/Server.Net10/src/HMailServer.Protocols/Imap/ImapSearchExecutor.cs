using HMailServer.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSearchExecutor
{
    private const int DocumentBatchSize = 128;

    private readonly IMessageSearchIndex _searchIndex;
    private readonly IImapSequenceNumberResolver? _sequenceNumberResolver;
    private readonly IMessageSearchDocumentSource? _documentSource;
    private readonly IMessageIndexingAdministrationStore? _indexingAdministrationStore;

    public ImapSearchExecutor(
        IMessageSearchIndex searchIndex,
        IImapSequenceNumberResolver? sequenceNumberResolver = null,
        IMessageSearchDocumentSource? documentSource = null,
        IMessageIndexingAdministrationStore? indexingAdministrationStore = null)
    {
        _searchIndex = searchIndex;
        _sequenceNumberResolver = sequenceNumberResolver;
        _documentSource = documentSource;
        _indexingAdministrationStore = indexingAdministrationStore;
    }

    public async ValueTask<string> ExecuteAsync(
        ImapSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identifiers = request.ReturnUid
            ? await SelectUidsAsync(request, cancellationToken).ConfigureAwait(false)
            : await ResolveSequenceNumbersAsync(request, cancellationToken).ConfigureAwait(false);

        return ImapSearchResultFormatter.Format(identifiers);
    }

    private async ValueTask<IReadOnlyList<long>> SelectUidsAsync(
        ImapSearchRequest request,
        CancellationToken cancellationToken)
    {
        var identifiers = new List<long>();
        await foreach (var identity in SearchMatchesAsync(request, cancellationToken).ConfigureAwait(false))
        {
            identifiers.Add(identity.Uid);
        }

        return identifiers;
    }

    private async ValueTask<IReadOnlyList<long>> ResolveSequenceNumbersAsync(
        ImapSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (_sequenceNumberResolver is null)
        {
            throw new InvalidOperationException("Non-UID IMAP SEARCH requires a mailbox sequence number resolver.");
        }

        var sequenceNumbers = await _sequenceNumberResolver
            .ResolveMailboxSequenceNumbersAsync(request.AccountId, request.FolderId, cancellationToken)
            .ConfigureAwait(false);

        var identifiers = new List<long>();
        await foreach (var identity in SearchMatchesAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (!sequenceNumbers.TryGetValue(identity.MessageId, out var sequenceNumber))
            {
                throw new InvalidOperationException(
                    $"Message {identity.MessageId} was returned by SEARCH but is not present in the mailbox sequence snapshot.");
            }

            identifiers.Add(sequenceNumber);
        }

        return identifiers;
    }

    private async IAsyncEnumerable<MessageIdentity> SearchMatchesAsync(
        ImapSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subjectTerms = request.GetSubjectTerms();
        var headerTerms = request.GetHeaderTerms();
        var bodyTerms = request.GetBodyTerms();
        var anyTerms = request.GetAnyTerms();
        var useFileTextFallback = await ShouldUseFileTextFallbackAsync(
            headerTerms.Count + bodyTerms.Count + anyTerms.Count,
            cancellationToken).ConfigureAwait(false);

        if ((subjectTerms.Count > 0 || useFileTextFallback) && _documentSource is null)
        {
            throw new InvalidOperationException("File-backed IMAP SEARCH requires a message file document source.");
        }

        var candidateRequest = useFileTextFallback
            ? request with
            {
                HeaderText = null,
                HeaderTerms = Array.Empty<string>(),
                BodyText = null,
                BodyTerms = Array.Empty<string>(),
                AnyText = null,
                AnyTerms = Array.Empty<string>()
            }
            : request;

        if (subjectTerms.Count == 0 && !useFileTextFallback)
        {
            await foreach (var identity in _searchIndex.SearchAsync(candidateRequest, cancellationToken).ConfigureAwait(false))
            {
                yield return identity;
            }

            yield break;
        }

        var batch = new List<MessageIdentity>(DocumentBatchSize);
        await foreach (var identity in _searchIndex.SearchAsync(candidateRequest, cancellationToken).ConfigureAwait(false))
        {
            batch.Add(identity);
            if (batch.Count < DocumentBatchSize)
            {
                continue;
            }

            await foreach (var match in FilterFileMatchesAsync(
                batch,
                subjectTerms,
                headerTerms,
                bodyTerms,
                anyTerms,
                useFileTextFallback,
                cancellationToken).ConfigureAwait(false))
            {
                yield return match;
            }

            batch.Clear();
        }

        if (batch.Count > 0)
        {
            await foreach (var match in FilterFileMatchesAsync(
                batch,
                subjectTerms,
                headerTerms,
                bodyTerms,
                anyTerms,
                useFileTextFallback,
                cancellationToken).ConfigureAwait(false))
            {
                yield return match;
            }
        }
    }

    private async IAsyncEnumerable<MessageIdentity> FilterFileMatchesAsync(
        IReadOnlyList<MessageIdentity> identities,
        IReadOnlyList<string> subjectTerms,
        IReadOnlyList<string> headerTerms,
        IReadOnlyList<string> bodyTerms,
        IReadOnlyList<string> anyTerms,
        bool useFileTextFallback,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_documentSource is IMessageSearchDocumentBatchSource batchSource)
        {
            var resultCount = 0;
            await foreach (var document in batchSource
                .TryLoadBatchAsync(identities, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (resultCount >= identities.Count)
                {
                    throw CreateBatchCountException(identities.Count, resultCount + 1);
                }

                var identity = identities[resultCount++];
                if (DocumentMatches(
                    document,
                    subjectTerms,
                    headerTerms,
                    bodyTerms,
                    anyTerms,
                    useFileTextFallback))
                {
                    yield return identity;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (resultCount != identities.Count)
            {
                throw CreateBatchCountException(identities.Count, resultCount);
            }

            yield break;
        }

        foreach (var identity in identities)
        {
            var document = await _documentSource!
                .TryLoadAsync(identity, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (DocumentMatches(
                document,
                subjectTerms,
                headerTerms,
                bodyTerms,
                anyTerms,
                useFileTextFallback))
            {
                yield return identity;
            }
        }
    }

    private static bool DocumentMatches(
        MessageSearchDocument? document,
        IReadOnlyList<string> subjectTerms,
        IReadOnlyList<string> headerTerms,
        IReadOnlyList<string> bodyTerms,
        IReadOnlyList<string> anyTerms,
        bool useFileTextFallback)
    {
        if (document is null)
        {
            return false;
        }

        if (subjectTerms.Count > 0 && !SubjectMatches(document.SubjectText, subjectTerms))
        {
            return false;
        }

        if (useFileTextFallback
            && (!HeaderMatches(document.FileSearchHeaderText, headerTerms)
                || !BodyMatches(document, bodyTerms)
                || !TextMatches(document, anyTerms)))
        {
            return false;
        }

        return true;
    }

    private static InvalidOperationException CreateBatchCountException(int expectedCount, int actualCount) =>
        new($"Message document batch source returned {actualCount} results for {expectedCount} identities.");

    private static bool TextMatches(MessageSearchDocument document, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (!document.FileSearchHeaderText.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !document.FileSearchPlainBodyText.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !document.FileSearchHtmlBodyText.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HeaderMatches(string headerText, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (!headerText.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool BodyMatches(MessageSearchDocument document, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (!document.FileSearchPlainBodyText.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !document.FileSearchHtmlBodyText.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SubjectMatches(string subject, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (!subject.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask<bool> ShouldUseFileTextFallbackAsync(
        int textTermCount,
        CancellationToken cancellationToken)
    {
        if (textTermCount == 0 || _indexingAdministrationStore is null)
        {
            return false;
        }

        if (!await _indexingAdministrationStore
            .IsEnabledAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        var status = await _indexingAdministrationStore
            .GetStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        return status.TotalIndexedCount < status.TotalMessageCount;
    }
}
