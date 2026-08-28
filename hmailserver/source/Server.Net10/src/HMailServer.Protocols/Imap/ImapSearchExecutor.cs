using HMailServer.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSearchExecutor
{
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
        var anyTerms = request.GetAnyTerms();
        var useFileTextFallback = anyTerms.Count > 0
            && _indexingAdministrationStore is not null
            && !await _indexingAdministrationStore
                .IsEnabledAsync(cancellationToken)
                .ConfigureAwait(false);

        if ((subjectTerms.Count > 0 || useFileTextFallback) && _documentSource is null)
        {
            throw new InvalidOperationException("File-backed IMAP SEARCH requires a message file document source.");
        }

        var candidateRequest = useFileTextFallback
            ? request with { AnyText = null, AnyTerms = Array.Empty<string>() }
            : request;

        await foreach (var identity in _searchIndex.SearchAsync(candidateRequest, cancellationToken).ConfigureAwait(false))
        {
            if (subjectTerms.Count == 0 && !useFileTextFallback)
            {
                yield return identity;
                continue;
            }

            var document = await _documentSource!
                .TryLoadAsync(identity, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (document is null)
            {
                continue;
            }

            if (subjectTerms.Count > 0 && !SubjectMatches(document.SubjectText, subjectTerms))
            {
                continue;
            }

            if (useFileTextFallback && !TextMatches(document, anyTerms))
            {
                continue;
            }

            yield return identity;
        }
    }

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
}
