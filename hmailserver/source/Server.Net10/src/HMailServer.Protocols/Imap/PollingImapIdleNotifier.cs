using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class PollingImapIdleNotifier : IImapIdleNotifier
{
    private static readonly ImapStatusItem[] WatchedStatusItems =
    [
        ImapStatusItem.Messages,
        ImapStatusItem.Recent
    ];

    private readonly IImapMailboxDiscoveryStore _mailboxDiscoveryStore;
    private readonly ImapIdlePollingOptions _options;

    public PollingImapIdleNotifier(
        IImapMailboxDiscoveryStore mailboxDiscoveryStore,
        ImapIdlePollingOptions options)
    {
        _mailboxDiscoveryStore = mailboxDiscoveryStore;
        _options = options;
        if (_options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "IMAP IDLE poll interval must be positive.");
        }
    }

    public async IAsyncEnumerable<ImapIdleEvent> WatchAsync(
        ImapIdleWatchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lastExists = request.KnownExists;
        var lastEmittedRecent = request.KnownRecent;
        long? databaseRecentBaseline = null;
        using var timer = new PeriodicTimer(_options.PollInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var status = await _mailboxDiscoveryStore
                .GetStatusAsync(request.AccountId, request.MailboxName, WatchedStatusItems, cancellationToken)
                .ConfigureAwait(false);

            if (status is null)
            {
                continue;
            }

            if (TryGetValue(status, ImapStatusItem.Messages, out var exists) && exists != lastExists)
            {
                lastExists = exists;
                yield return new ImapIdleEvent(ImapIdleEventKind.Exists, exists);
            }

            if (TryGetValue(status, ImapStatusItem.Recent, out var recent))
            {
                databaseRecentBaseline ??= Math.Min(recent, request.KnownRecent);
                var totalRecent = request.KnownRecent + Math.Max(0, recent - databaseRecentBaseline.Value);
                if (totalRecent != lastEmittedRecent)
                {
                    lastEmittedRecent = totalRecent;
                    yield return new ImapIdleEvent(ImapIdleEventKind.Recent, totalRecent);
                }
            }
        }
    }

    private static bool TryGetValue(
        ImapMailboxStatus status,
        ImapStatusItem item,
        out long value) =>
        status.Values.TryGetValue(item, out value);
}
