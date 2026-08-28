using HMailServer.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSearchExecutor
{
    private readonly IMessageSearchIndex _searchIndex;
    private readonly IImapSequenceNumberResolver? _sequenceNumberResolver;
    private readonly IMessageSearchDocumentSource? _documentSource;

    public ImapSearchExecutor(
        IMessageSearchIndex searchIndex,
        IImapSequenceNumberResolver? sequenceNumberResolver = null,
        IMessageSearchDocumentSource? documentSource = null)
    {
        _searchIndex = searchIndex;
        _sequenceNumberResolver = sequenceNumberResolver;
        _documentSource = documentSource;
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
        if (subjectTerms.Count > 0 && _documentSource is null)
        {
            throw new InvalidOperationException("IMAP SEARCH SUBJECT requires a message file document source.");
        }

        await foreach (var identity in _searchIndex.SearchAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (subjectTerms.Count == 0)
            {
                yield return identity;
                continue;
            }

            var document = await _documentSource!
                .TryLoadAsync(identity, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (document is not null && SubjectMatches(document.SubjectText, subjectTerms))
            {
                yield return identity;
            }
        }
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
