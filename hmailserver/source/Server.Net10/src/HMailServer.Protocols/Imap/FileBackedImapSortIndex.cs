using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class FileBackedImapSortIndex : IMessageSortIndex
{
    private const int DocumentBatchSize = 128;

    private readonly IMessageSortIndex _inner;
    private readonly IMessageSearchDocumentSource _documentSource;
    private readonly IMessageIndexingAdministrationStore _indexingAdministrationStore;

    public FileBackedImapSortIndex(
        IMessageSortIndex inner,
        IMessageSearchDocumentSource documentSource,
        IMessageIndexingAdministrationStore indexingAdministrationStore)
    {
        _inner = inner;
        _documentSource = documentSource;
        _indexingAdministrationStore = indexingAdministrationStore;
    }

    public async IAsyncEnumerable<MessageIdentity> SortAsync(
        ImapSortRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var searchRequest = request.SearchRequest;
        var subjectTerms = searchRequest.GetSubjectTerms();
        var headerTerms = searchRequest.GetHeaderTerms();
        var bodyTerms = searchRequest.GetBodyTerms();
        var anyTerms = searchRequest.GetAnyTerms();
        var useFileTextFallback = await ShouldUseFileTextFallbackAsync(
            headerTerms.Count + subjectTerms.Count + bodyTerms.Count + anyTerms.Count,
            cancellationToken).ConfigureAwait(false);

        if (!useFileTextFallback)
        {
            await foreach (var identity in _inner.SortAsync(request, cancellationToken).ConfigureAwait(false))
            {
                yield return identity;
            }

            yield break;
        }

        var candidateRequest = request with
        {
            SearchRequest = searchRequest with
            {
                HeaderText = null,
                HeaderTerms = Array.Empty<string>(),
                BodyText = null,
                BodyTerms = Array.Empty<string>(),
                AnyText = null,
                AnyTerms = Array.Empty<string>(),
                SubjectTerms = Array.Empty<string>()
            }
        };

        var batch = new List<MessageIdentity>(DocumentBatchSize);
        await foreach (var identity in _inner.SortAsync(candidateRequest, cancellationToken).ConfigureAwait(false))
        {
            batch.Add(identity);
            if (batch.Count < DocumentBatchSize)
            {
                continue;
            }

            await foreach (var match in FilterBatchAsync(
                batch,
                subjectTerms,
                headerTerms,
                bodyTerms,
                anyTerms,
                cancellationToken).ConfigureAwait(false))
            {
                yield return match;
            }

            batch.Clear();
        }

        if (batch.Count > 0)
        {
            await foreach (var match in FilterBatchAsync(
                batch,
                subjectTerms,
                headerTerms,
                bodyTerms,
                anyTerms,
                cancellationToken).ConfigureAwait(false))
            {
                yield return match;
            }
        }
    }

    private async IAsyncEnumerable<MessageIdentity> FilterBatchAsync(
        IReadOnlyList<MessageIdentity> identities,
        IReadOnlyList<string> subjectTerms,
        IReadOnlyList<string> headerTerms,
        IReadOnlyList<string> bodyTerms,
        IReadOnlyList<string> anyTerms,
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
                    throw new InvalidOperationException(
                        $"Message document batch source returned more than {identities.Count} results.");
                }

                var identity = identities[resultCount++];
                if (DocumentMatches(document, subjectTerms, headerTerms, bodyTerms, anyTerms))
                {
                    yield return identity;
                }
            }

            if (resultCount != identities.Count)
            {
                throw new InvalidOperationException(
                    $"Message document batch source returned {resultCount} results for {identities.Count} identities.");
            }

            yield break;
        }

        foreach (var identity in identities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await _documentSource
                .TryLoadAsync(identity, cancellationToken)
                .ConfigureAwait(false);
            if (DocumentMatches(document, subjectTerms, headerTerms, bodyTerms, anyTerms))
            {
                yield return identity;
            }
        }
    }

    private async ValueTask<bool> ShouldUseFileTextFallbackAsync(
        int textTermCount,
        CancellationToken cancellationToken)
    {
        if (textTermCount == 0)
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
        return !status.IsFullTextReady || status.TotalIndexedCount < status.TotalMessageCount;
    }

    private static bool DocumentMatches(
        MessageSearchDocument? document,
        IReadOnlyList<string> subjectTerms,
        IReadOnlyList<string> headerTerms,
        IReadOnlyList<string> bodyTerms,
        IReadOnlyList<string> anyTerms)
    {
        if (document is null)
        {
            return false;
        }

        return SubjectMatches(document.SubjectText, subjectTerms)
            && HeaderMatches(document.FileSearchHeaderText, headerTerms)
            && BodyMatches(document, bodyTerms)
            && TextMatches(document, anyTerms);
    }

    private static bool SubjectMatches(string subject, IReadOnlyList<string> terms) =>
        terms.All(term => subject.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool HeaderMatches(string header, IReadOnlyList<string> terms) =>
        terms.All(term => header.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool BodyMatches(MessageSearchDocument document, IReadOnlyList<string> terms) =>
        terms.All(term => document.FileSearchPlainBodyText.Contains(term, StringComparison.OrdinalIgnoreCase)
            || document.FileSearchHtmlBodyText.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool TextMatches(MessageSearchDocument document, IReadOnlyList<string> terms) =>
        terms.All(term => document.FileSearchHeaderText.Contains(term, StringComparison.OrdinalIgnoreCase)
            || document.FileSearchPlainBodyText.Contains(term, StringComparison.OrdinalIgnoreCase)
            || document.FileSearchHtmlBodyText.Contains(term, StringComparison.OrdinalIgnoreCase));
}
