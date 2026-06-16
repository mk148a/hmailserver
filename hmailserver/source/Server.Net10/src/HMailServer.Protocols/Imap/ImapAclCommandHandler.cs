using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapAclCommandHandler
{
    private readonly IImapAclStore _aclStore;

    public ImapAclCommandHandler(IImapAclStore aclStore)
    {
        _aclStore = aclStore;
    }

    public async ValueTask<string> HandleAsync(
        int requesterAccountId,
        string tag,
        string command,
        string arguments,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requesterAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        IReadOnlyList<string> parsedArguments;
        try
        {
            parsedArguments = ImapCommandArguments.Parse(arguments);
        }
        catch (ImapSearchParseException ex)
        {
            return TaggedBad(tag, ex.Message);
        }

        return command.ToUpperInvariant() switch
        {
            "GETACL" => await HandleGetAclAsync(requesterAccountId, tag, parsedArguments, cancellationToken).ConfigureAwait(false),
            "MYRIGHTS" => await HandleMyRightsAsync(requesterAccountId, tag, parsedArguments, cancellationToken).ConfigureAwait(false),
            "LISTRIGHTS" => await HandleListRightsAsync(requesterAccountId, tag, parsedArguments, cancellationToken).ConfigureAwait(false),
            "SETACL" => await HandleSetAclAsync(requesterAccountId, tag, parsedArguments, cancellationToken).ConfigureAwait(false),
            "DELETEACL" => await HandleDeleteAclAsync(requesterAccountId, tag, parsedArguments, cancellationToken).ConfigureAwait(false),
            _ => TaggedBad(tag, "Unsupported ACL command")
        };
    }

    private async ValueTask<string> HandleGetAclAsync(
        int requesterAccountId,
        string tag,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != 1)
        {
            return TaggedBad(tag, "GETACL requires one mailbox name");
        }

        var result = await _aclStore.GetAclAsync(requesterAccountId, arguments[0], cancellationToken).ConfigureAwait(false);
        if (result.Status != ImapAclCommandStatus.Success)
        {
            return FormatFailure(tag, result.Status, "GETACL");
        }

        return ImapAclResponseFormatter.FormatGetAcl(result) + $"{SanitizeAtom(tag)} OK GETACL completed\r\n";
    }

    private async ValueTask<string> HandleMyRightsAsync(
        int requesterAccountId,
        string tag,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != 1)
        {
            return TaggedBad(tag, "MYRIGHTS requires one mailbox name");
        }

        var result = await _aclStore.GetMyRightsAsync(requesterAccountId, arguments[0], cancellationToken).ConfigureAwait(false);
        if (result.Status != ImapAclCommandStatus.Success)
        {
            return FormatFailure(tag, result.Status, "MYRIGHTS");
        }

        return ImapAclResponseFormatter.FormatMyRights(result) + $"{SanitizeAtom(tag)} OK MYRIGHTS completed\r\n";
    }

    private async ValueTask<string> HandleListRightsAsync(
        int requesterAccountId,
        string tag,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != 2)
        {
            return TaggedBad(tag, "LISTRIGHTS requires mailbox name and identifier");
        }

        var result = await _aclStore.GetMyRightsAsync(requesterAccountId, arguments[0], cancellationToken).ConfigureAwait(false);
        if (result.Status != ImapAclCommandStatus.Success)
        {
            return FormatFailure(tag, result.Status, "LISTRIGHTS");
        }

        return ImapAclResponseFormatter.FormatListRights(result.MailboxName, arguments[1]) +
            $"{SanitizeAtom(tag)} OK LISTRIGHTS completed\r\n";
    }

    private async ValueTask<string> HandleSetAclAsync(
        int requesterAccountId,
        string tag,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != 3)
        {
            return TaggedBad(tag, "SETACL requires mailbox name, identifier, and rights");
        }

        if (!ImapAclRights.TryParseChange(arguments[2], out var rightsChange))
        {
            return TaggedBad(tag, "SETACL contains an invalid access right");
        }

        var result = await _aclStore
            .SetAclAsync(requesterAccountId, arguments[0], arguments[1], rightsChange, cancellationToken)
            .ConfigureAwait(false);
        return result.Status == ImapAclCommandStatus.Success
            ? $"{SanitizeAtom(tag)} OK SETACL completed\r\n"
            : FormatFailure(tag, result.Status, "SETACL");
    }

    private async ValueTask<string> HandleDeleteAclAsync(
        int requesterAccountId,
        string tag,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != 2)
        {
            return TaggedBad(tag, "DELETEACL requires mailbox name and identifier");
        }

        var result = await _aclStore
            .DeleteAclAsync(requesterAccountId, arguments[0], arguments[1], cancellationToken)
            .ConfigureAwait(false);
        return result.Status == ImapAclCommandStatus.Success
            ? $"{SanitizeAtom(tag)} OK DELETEACL completed\r\n"
            : FormatFailure(tag, result.Status, "DELETEACL");
    }

    private static string FormatFailure(string tag, ImapAclCommandStatus status, string command) =>
        status switch
        {
            ImapAclCommandStatus.AclDisabled => TaggedBad(tag, "ACL is not enabled"),
            ImapAclCommandStatus.MailboxNotFound => TaggedNo(tag, "Folder could not be found."),
            ImapAclCommandStatus.PermissionDenied => TaggedNo(tag, $"{command} permission denied"),
            ImapAclCommandStatus.PrivateMailboxNotSupported => TaggedNo(tag, "It is not possible to set permissions for account folders."),
            ImapAclCommandStatus.IdentifierNotFound => TaggedNo(tag, $"{command} identifier not found"),
            _ => TaggedNo(tag, $"{command} failed")
        };

    private static string TaggedBad(string tag, string response) =>
        $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(response)}\r\n";

    private static string TaggedNo(string tag, string response) =>
        $"{SanitizeAtom(tag)} NO {SanitizeResponseText(response)}\r\n";

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
