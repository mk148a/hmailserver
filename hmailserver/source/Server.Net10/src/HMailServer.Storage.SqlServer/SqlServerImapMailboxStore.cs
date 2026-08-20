using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapMailboxStore : IImapMailboxStore, IImapSelectedMailboxAuthorization, IImapMailboxDiscoveryStore, IImapAclStore, IImapMailboxSubscriptionStore
{
    public const string FindChildFolderSql = """
SELECT TOP (1)
    folderid,
    folderaccountid,
    folderparentid,
    foldername,
    folderissubscribed,
    foldercreationtime,
    foldercurrentuid
FROM hm_imapfolders
WHERE
    folderaccountid = @FolderAccountId
    AND folderparentid = @ParentFolderId
    AND LOWER(foldername) = LOWER(@FolderName);
""";

    public const string SelectFolderByIdSql = """
SELECT TOP (1)
    folderid,
    folderaccountid,
    folderparentid,
    foldername,
    folderissubscribed,
    foldercreationtime,
    foldercurrentuid
FROM hm_imapfolders
WHERE
    folderaccountid = @FolderAccountId
    AND folderid = @FolderId;
""";

    public const string SelectMailboxCountersSql = """
SELECT
    COUNT_BIG(m.messageid) AS messagecount,
    CONVERT(bigint, COALESCE(SUM(CASE WHEN (m.messageflags & 32) = 32 THEN 1 ELSE 0 END), 0)) AS recentcount,
    MIN(CASE WHEN (m.messageflags & 1) = 0 THEN m.messageuid ELSE NULL END) AS firstunseenuid,
    CONVERT(bigint, COALESCE(SUM(CASE WHEN (m.messageflags & 1) = 0 THEN 1 ELSE 0 END), 0)) AS unseencount
FROM hm_messages AS m
WHERE
    m.messageaccountid = @MessageAccountId
    AND m.messagefolderid = @FolderId
    AND m.messagetype = 2;
""";

    public const string UpdateMailboxSubscriptionSql = """
UPDATE hm_imapfolders
SET folderissubscribed = @Subscribed
WHERE
    folderid = @FolderId
    AND folderaccountid = @FolderAccountId;
""";

    public const string ListFoldersSql = """
SELECT
    folderid,
    folderaccountid,
    folderparentid,
    foldername,
    folderissubscribed,
    foldercreationtime,
    foldercurrentuid
FROM hm_imapfolders
WHERE folderaccountid = @FolderAccountId
ORDER BY folderparentid ASC, foldername ASC, folderid ASC;
""";

    public const string SelectAclPermissionsSql = """
SELECT
    aclid,
    aclpermissiontype,
    aclpermissiongroupid,
    aclpermissionaccountid,
    aclvalue
FROM hm_acl
WHERE aclsharefolderid = @FolderId
ORDER BY aclid ASC;
""";

    public const string IsGroupMemberSql = """
SELECT TOP (1) 1
FROM hm_group_members
WHERE
    membergroupid = @GroupId
    AND memberaccountid = @AccountId;
""";

    public const string SelectAclEntriesSql = """
SELECT
    acl.aclid,
    acl.aclpermissiontype,
    acl.aclpermissiongroupid,
    acl.aclpermissionaccountid,
    acl.aclvalue,
    CASE
        WHEN acl.aclpermissiontype = 2 THEN N'Anyone'
        WHEN acl.aclpermissiontype = 1 THEN COALESCE(g.groupname, CONVERT(nvarchar(32), acl.aclpermissiongroupid))
        ELSE COALESCE(a.accountaddress, CONVERT(nvarchar(32), acl.aclpermissionaccountid))
    END AS identifier
FROM hm_acl AS acl
LEFT JOIN hm_accounts AS a
    ON acl.aclpermissiontype = 0
    AND a.accountid = acl.aclpermissionaccountid
LEFT JOIN hm_groups AS g
    ON acl.aclpermissiontype = 1
    AND g.groupid = acl.aclpermissiongroupid
WHERE acl.aclsharefolderid = @FolderId
ORDER BY acl.aclid ASC;
""";

    public const string SelectAccountPrincipalSql = """
SELECT TOP (1) accountid
FROM hm_accounts
WHERE LOWER(accountaddress) = LOWER(@Identifier);
""";

    public const string SelectGroupPrincipalSql = """
SELECT TOP (1) groupid
FROM hm_groups
WHERE LOWER(groupname) = LOWER(@Identifier);
""";

    public const string SelectAclValueSql = """
SELECT TOP (1) aclvalue
FROM hm_acl
WHERE
    aclsharefolderid = @FolderId
    AND aclpermissiontype = @PermissionType
    AND aclpermissiongroupid = @GroupId
    AND aclpermissionaccountid = @AccountId;
""";

    public const string UpsertAclSql = """
UPDATE hm_acl
SET aclvalue = @AclValue
WHERE
    aclsharefolderid = @FolderId
    AND aclpermissiontype = @PermissionType
    AND aclpermissiongroupid = @GroupId
    AND aclpermissionaccountid = @AccountId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO hm_acl
    (
        aclsharefolderid,
        aclpermissiontype,
        aclpermissiongroupid,
        aclpermissionaccountid,
        aclvalue
    )
    VALUES
    (
        @FolderId,
        @PermissionType,
        @GroupId,
        @AccountId,
        @AclValue
    );
END;
""";

    public const string DeleteAclSql = """
DELETE FROM hm_acl
WHERE
    aclsharefolderid = @FolderId
    AND aclpermissiontype = @PermissionType
    AND aclpermissiongroupid = @GroupId
    AND aclpermissionaccountid = @AccountId;
""";

    private const long AclLookup = ImapAclRights.Lookup;
    private const long AclRead = ImapAclRights.Read;
    private const long AclWriteSeen = ImapAclRights.WriteSeen;
    private const long AclWriteOthers = ImapAclRights.WriteOthers;
    private const long AclInsert = ImapAclRights.Insert;
    private const long AclWriteDeleted = ImapAclRights.WriteDeleted;
    private const long AclExpunge = ImapAclRights.Expunge;
    private const long AclAdminister = ImapAclRights.Administer;

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerImapMailboxStoreOptions _options;
    private readonly IImapFolderChangeTracker _changeTracker;

    public SqlServerImapMailboxStore(
        SqlServerConnectionFactory connectionFactory,
        SqlServerImapMailboxStoreOptions? options = null,
        IImapFolderChangeTracker? changeTracker = null)
    {
        _connectionFactory = connectionFactory;
        _options = options ?? new SqlServerImapMailboxStoreOptions();
        _changeTracker = changeTracker ?? ImapFolderChangeTracker.Shared;
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.HierarchyDelimiter);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.PublicFolderName);
    }

    public async ValueTask<ImapMailboxSelection?> RevalidateSelectedMailboxAsync(
        int requesterAccountId,
        ImapMailboxSelection selectedMailbox,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requesterAccountId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var folder = await LoadFolderByIdAsync(
            connection,
            selectedMailbox.AccountId,
            selectedMailbox.FolderId,
            cancellationToken).ConfigureAwait(false);
        if (folder is null || folder.FolderAccountId != selectedMailbox.AccountId)
        {
            return null;
        }

        var access = await ResolveAccessAsync(
            connection,
            requesterAccountId,
            folder,
            isPublicFolder: folder.FolderAccountId == 0,
            cancellationToken).ConfigureAwait(false);
        if (!access.CanRead)
        {
            return null;
        }

        return selectedMailbox with
        {
            IsReadOnly = selectedMailbox.IsReadOnly || !access.CanWrite
        };
    }

    public async ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
        int accountId,
        string mailboxName,
        bool readOnly,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxName);

        var path = SqlServerImapMailboxPath.Parse(
            mailboxName.Trim(),
            _options.HierarchyDelimiter,
            _options.PublicFolderName);
        if (path is null)
        {
            return null;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var folderAccountId = path.IsPublicFolder ? 0 : accountId;
        var folder = await ResolveFolderPathAsync(
            connection,
            folderAccountId,
            path.Segments,
            cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        var access = await ResolveAccessAsync(
            connection,
            requesterAccountId: accountId,
            folder,
            path.IsPublicFolder,
            cancellationToken).ConfigureAwait(false);
        if (!access.CanRead)
        {
            return null;
        }

        var counters = await LoadCountersAsync(
            connection,
            folder.FolderAccountId,
            folder.FolderId,
            cancellationToken).ConfigureAwait(false);

        return new ImapMailboxSelection(
            AccountId: folder.FolderAccountId,
            FolderId: folder.FolderId,
            Name: mailboxName.Trim(),
            Exists: counters.Exists,
            Recent: counters.Recent,
            UidValidity: GetUidValidity(folder.CreationTime),
            UidNext: folder.CurrentUid + 1,
            FirstUnseenUid: counters.FirstUnseenUid,
            IsReadOnly: readOnly || !access.CanWrite);
    }

    public async ValueTask<ImapMailboxSubscriptionResult> SetSubscribedAsync(
        int requesterAccountId,
        string mailboxName,
        bool subscribed,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requesterAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxName);

        var trimmedName = mailboxName.Trim();
        var path = SqlServerImapMailboxPath.Parse(
            trimmedName,
            _options.HierarchyDelimiter,
            _options.PublicFolderName);
        if (path is null)
        {
            return new ImapMailboxSubscriptionResult(ImapMailboxSubscriptionStatus.MailboxNotFound);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var folderAccountId = path.IsPublicFolder ? 0 : requesterAccountId;
        var folder = await ResolveFolderPathAsync(
            connection,
            folderAccountId,
            path.Segments,
            cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return new ImapMailboxSubscriptionResult(ImapMailboxSubscriptionStatus.MailboxNotFound);
        }

        if (!subscribed && folder.FolderAccountId == 0)
        {
            return new ImapMailboxSubscriptionResult(ImapMailboxSubscriptionStatus.PublicFolderNotSupported);
        }

        if (subscribed)
        {
            var access = await ResolveAccessAsync(
                connection,
                requesterAccountId,
                folder,
                path.IsPublicFolder,
                cancellationToken).ConfigureAwait(false);
            if (!access.CanLookup)
            {
                return new ImapMailboxSubscriptionResult(ImapMailboxSubscriptionStatus.PermissionDenied);
            }
        }

        await using var command = new SqlCommand(UpdateMailboxSubscriptionSql, connection);
        command.Parameters.Add("@FolderAccountId", SqlDbType.Int).Value = folder.FolderAccountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folder.FolderId;
        command.Parameters.Add("@Subscribed", SqlDbType.Int).Value = subscribed ? 1 : 0;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1
            ? ImapMailboxSubscriptionResult.Success()
            : new ImapMailboxSubscriptionResult(ImapMailboxSubscriptionStatus.Failed);
    }

    public async IAsyncEnumerable<ImapMailboxListEntry> ListMailboxesAsync(
        int accountId,
        string referenceName,
        string mailboxPattern,
        bool subscribedOnly,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentNullException.ThrowIfNull(referenceName);
        ArgumentNullException.ThrowIfNull(mailboxPattern);

        var folderSpecifier = referenceName + mailboxPattern;
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var privateFolders = await LoadFolderRowsAsync(connection, accountId, cancellationToken).ConfigureAwait(false);
        foreach (var entry in BuildListEntries(
            privateFolders,
            prefix: string.Empty,
            folderSpecifier,
            _options.HierarchyDelimiter,
            subscribedOnly))
        {
            yield return entry;
        }

        var publicFolders = await LoadFolderRowsAsync(connection, folderAccountId: 0, cancellationToken).ConfigureAwait(false);
        var publicFoldersById = publicFolders.ToDictionary(static folder => folder.FolderId);
        var publicParentIds = publicFolders
            .Select(static folder => folder.ParentFolderId)
            .Where(static parentId => parentId >= 0)
            .ToHashSet();
        var anyVisiblePublicFolder = false;

        foreach (var folder in publicFolders)
        {
            var access = await ResolveAccessAsync(
                connection,
                requesterAccountId: accountId,
                folder,
                isPublicFolder: true,
                cancellationToken).ConfigureAwait(false);
            if (!access.CanLookup)
            {
                continue;
            }

            anyVisiblePublicFolder = true;
            if (subscribedOnly && !folder.IsSubscribed)
            {
                continue;
            }

            var name = BuildMailboxName(folder, publicFoldersById, _options.PublicFolderName, _options.HierarchyDelimiter);
            if (!WildcardMatch(name, folderSpecifier, _options.HierarchyDelimiter))
            {
                continue;
            }

            yield return new ImapMailboxListEntry(
                name,
                HasChildren: publicParentIds.Contains(folder.FolderId),
                IsSelectable: true,
                IsSubscribed: folder.IsSubscribed);
        }

        if (anyVisiblePublicFolder &&
            WildcardMatch(_options.PublicFolderName, folderSpecifier, _options.HierarchyDelimiter))
        {
            yield return new ImapMailboxListEntry(
                _options.PublicFolderName,
                HasChildren: true,
                IsSelectable: false,
                IsSubscribed: true);
        }
    }

    public async ValueTask<ImapMailboxStatus?> GetStatusAsync(
        int accountId,
        string mailboxName,
        IReadOnlyList<ImapStatusItem> items,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxName);
        ArgumentNullException.ThrowIfNull(items);

        var trimmedName = mailboxName.Trim();
        var path = SqlServerImapMailboxPath.Parse(
            trimmedName,
            _options.HierarchyDelimiter,
            _options.PublicFolderName);
        if (path is null)
        {
            return null;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var folderAccountId = path.IsPublicFolder ? 0 : accountId;
        var folder = await ResolveFolderPathAsync(
            connection,
            folderAccountId,
            path.Segments,
            cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        var access = await ResolveAccessAsync(
            connection,
            requesterAccountId: accountId,
            folder,
            path.IsPublicFolder,
            cancellationToken).ConfigureAwait(false);
        if (!access.CanRead)
        {
            return null;
        }

        var counters = await LoadCountersAsync(
            connection,
            folder.FolderAccountId,
            folder.FolderId,
            cancellationToken).ConfigureAwait(false);

        var values = new Dictionary<ImapStatusItem, long>();
        foreach (var item in items.Distinct())
        {
            values[item] = item switch
            {
                ImapStatusItem.Messages => counters.Exists,
                ImapStatusItem.Recent => counters.Recent,
                ImapStatusItem.UidNext => folder.CurrentUid + 1,
                ImapStatusItem.UidValidity => GetUidValidity(folder.CreationTime),
                ImapStatusItem.Unseen => counters.Unseen,
                _ => throw new ArgumentOutOfRangeException(nameof(items), item, "Unknown IMAP STATUS data item.")
            };
        }

        return new ImapMailboxStatus(trimmedName, values);
    }

    public async ValueTask<ImapAclListResult> GetAclAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var context = await ResolveAclMailboxAsync(
            connection,
            requesterAccountId,
            mailboxName,
            requirePublicMailbox: false,
            requireAdminister: false,
            cancellationToken).ConfigureAwait(false);
        if (context.Status != ImapAclCommandStatus.Success || context.Folder is null)
        {
            return ImapAclListResult.Failure(context.Status);
        }

        var entries = await LoadAclEntriesAsync(connection, context.Folder.FolderId, cancellationToken).ConfigureAwait(false);
        return new ImapAclListResult(context.Status, context.MailboxName, entries);
    }

    public async ValueTask<ImapAclRightsResult> GetMyRightsAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var context = await ResolveAclMailboxAsync(
            connection,
            requesterAccountId,
            mailboxName,
            requirePublicMailbox: false,
            requireAdminister: false,
            cancellationToken).ConfigureAwait(false);
        if (context.Status != ImapAclCommandStatus.Success || context.Access is null)
        {
            return ImapAclRightsResult.Failure(context.Status);
        }

        return new ImapAclRightsResult(
            context.Status,
            context.MailboxName,
            ImapAclRights.Format(context.Access.Value));
    }

    public async ValueTask<ImapAclMutationResult> SetAclAsync(
        int requesterAccountId,
        string mailboxName,
        string identifier,
        ImapAclRightsChange rightsChange,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentNullException.ThrowIfNull(rightsChange);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var context = await ResolveAclMailboxAsync(
            connection,
            requesterAccountId,
            mailboxName,
            requirePublicMailbox: true,
            requireAdminister: true,
            cancellationToken).ConfigureAwait(false);
        if (context.Status != ImapAclCommandStatus.Success || context.Folder is null)
        {
            return new ImapAclMutationResult(context.Status);
        }

        var principal = await ResolveAclPrincipalAsync(connection, identifier, cancellationToken).ConfigureAwait(false);
        if (principal is null)
        {
            return new ImapAclMutationResult(ImapAclCommandStatus.IdentifierNotFound);
        }

        var existingRights = await LoadAclValueAsync(
            connection,
            context.Folder.FolderId,
            principal,
            cancellationToken).ConfigureAwait(false);
        var newRights = rightsChange.Apply(existingRights);
        await UpsertAclAsync(connection, context.Folder.FolderId, principal, newRights, cancellationToken).ConfigureAwait(false);
        _changeTracker.PublishAclChange(context.Folder.FolderId);
        return new ImapAclMutationResult(ImapAclCommandStatus.Success);
    }

    public async ValueTask<ImapAclMutationResult> DeleteAclAsync(
        int requesterAccountId,
        string mailboxName,
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var context = await ResolveAclMailboxAsync(
            connection,
            requesterAccountId,
            mailboxName,
            requirePublicMailbox: true,
            requireAdminister: true,
            cancellationToken).ConfigureAwait(false);
        if (context.Status != ImapAclCommandStatus.Success || context.Folder is null)
        {
            return new ImapAclMutationResult(context.Status);
        }

        var principal = await ResolveAclPrincipalAsync(connection, identifier, cancellationToken).ConfigureAwait(false);
        if (principal is null)
        {
            return new ImapAclMutationResult(ImapAclCommandStatus.IdentifierNotFound);
        }

        var deleted = await DeleteAclAsync(connection, context.Folder.FolderId, principal, cancellationToken).ConfigureAwait(false);
        if (deleted)
        {
            _changeTracker.PublishAclChange(context.Folder.FolderId);
        }
        return new ImapAclMutationResult(ImapAclCommandStatus.Success);
    }

    private async ValueTask<AclMailboxContext> ResolveAclMailboxAsync(
        SqlConnection connection,
        int requesterAccountId,
        string mailboxName,
        bool requirePublicMailbox,
        bool requireAdminister,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requesterAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxName);

        if (!_options.UseAcl)
        {
            return AclMailboxContext.Failure(ImapAclCommandStatus.AclDisabled);
        }

        var trimmedName = mailboxName.Trim();
        var path = SqlServerImapMailboxPath.Parse(
            trimmedName,
            _options.HierarchyDelimiter,
            _options.PublicFolderName);
        if (path is null)
        {
            return AclMailboxContext.Failure(ImapAclCommandStatus.MailboxNotFound);
        }

        if (requirePublicMailbox && !path.IsPublicFolder)
        {
            return AclMailboxContext.Failure(ImapAclCommandStatus.PrivateMailboxNotSupported);
        }

        var folderAccountId = path.IsPublicFolder ? 0 : requesterAccountId;
        var folder = await ResolveFolderPathAsync(
            connection,
            folderAccountId,
            path.Segments,
            cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return AclMailboxContext.Failure(ImapAclCommandStatus.MailboxNotFound);
        }

        var access = await ResolveAccessAsync(
            connection,
            requesterAccountId,
            folder,
            path.IsPublicFolder,
            cancellationToken).ConfigureAwait(false);
        if (requireAdminister && !access.CanAdminister)
        {
            return AclMailboxContext.Failure(ImapAclCommandStatus.PermissionDenied);
        }

        return new AclMailboxContext(
            ImapAclCommandStatus.Success,
            trimmedName,
            folder,
            access);
    }

    private static async ValueTask<IReadOnlyList<ImapAclEntry>> LoadAclEntriesAsync(
        SqlConnection connection,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectAclEntriesSql, connection);
        command.Parameters.Add("@FolderId", SqlDbType.BigInt).Value = folderId;
        var entries = new List<ImapAclEntry>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(
                new ImapAclEntry(
                    reader.GetString(5),
                    ImapAclRights.Format(reader.GetInt64(4))));
        }

        return entries;
    }

    private static async ValueTask<AclPrincipal?> ResolveAclPrincipalAsync(
        SqlConnection connection,
        string identifier,
        CancellationToken cancellationToken)
    {
        var trimmedIdentifier = identifier.Trim();
        if (trimmedIdentifier.Equals("Anyone", StringComparison.OrdinalIgnoreCase))
        {
            return new AclPrincipal(PermissionType: 2, GroupId: 0, AccountId: 0);
        }

        if (await LookupPrincipalIdAsync(connection, SelectAccountPrincipalSql, trimmedIdentifier, cancellationToken).ConfigureAwait(false) is { } accountId)
        {
            return new AclPrincipal(PermissionType: 0, GroupId: 0, AccountId: accountId);
        }

        if (await LookupPrincipalIdAsync(connection, SelectGroupPrincipalSql, trimmedIdentifier, cancellationToken).ConfigureAwait(false) is { } groupId)
        {
            return new AclPrincipal(PermissionType: 1, GroupId: groupId, AccountId: 0);
        }

        return null;
    }

    private static async ValueTask<long?> LookupPrincipalIdAsync(
        SqlConnection connection,
        string commandText,
        string identifier,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(commandText, connection);
        command.Parameters.Add("@Identifier", SqlDbType.NVarChar, 255).Value = identifier;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null ? null : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask<long> LoadAclValueAsync(
        SqlConnection connection,
        int folderId,
        AclPrincipal principal,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectAclValueSql, connection);
        AddAclPrincipalParameters(command, folderId, principal);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null ? 0 : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask UpsertAclAsync(
        SqlConnection connection,
        int folderId,
        AclPrincipal principal,
        long aclValue,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(UpsertAclSql, connection);
        AddAclPrincipalParameters(command, folderId, principal);
        command.Parameters.Add("@AclValue", SqlDbType.BigInt).Value = aclValue;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> DeleteAclAsync(
        SqlConnection connection,
        int folderId,
        AclPrincipal principal,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(DeleteAclSql, connection);
        AddAclPrincipalParameters(command, folderId, principal);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private static void AddAclPrincipalParameters(
        SqlCommand command,
        int folderId,
        AclPrincipal principal)
    {
        command.Parameters.Add("@FolderId", SqlDbType.BigInt).Value = folderId;
        command.Parameters.Add("@PermissionType", SqlDbType.TinyInt).Value = principal.PermissionType;
        command.Parameters.Add("@GroupId", SqlDbType.BigInt).Value = principal.GroupId;
        command.Parameters.Add("@AccountId", SqlDbType.BigInt).Value = principal.AccountId;
    }

    private static long GetUidValidity(DateTime creationTime)
    {
        var utc = DateTime.SpecifyKind(creationTime, DateTimeKind.Utc);
        var seconds = new DateTimeOffset(utc).ToUnixTimeSeconds();
        return Math.Max(1, seconds);
    }

    private static IEnumerable<ImapMailboxListEntry> BuildListEntries(
        IReadOnlyList<FolderRow> folders,
        string prefix,
        string folderSpecifier,
        string hierarchyDelimiter,
        bool subscribedOnly)
    {
        var foldersById = folders.ToDictionary(static folder => folder.FolderId);
        var parentIds = folders
            .Select(static folder => folder.ParentFolderId)
            .Where(static parentId => parentId >= 0)
            .ToHashSet();

        foreach (var folder in folders)
        {
            if (subscribedOnly && !folder.IsSubscribed)
            {
                continue;
            }

            var name = BuildMailboxName(folder, foldersById, prefix, hierarchyDelimiter);
            if (!WildcardMatch(name, folderSpecifier, hierarchyDelimiter))
            {
                continue;
            }

            yield return new ImapMailboxListEntry(
                name,
                HasChildren: parentIds.Contains(folder.FolderId),
                IsSelectable: true,
                IsSubscribed: folder.IsSubscribed);
        }
    }

    private async ValueTask<IReadOnlyList<FolderRow>> LoadFolderRowsAsync(
        SqlConnection connection,
        int folderAccountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(ListFoldersSql, connection);
        command.Parameters.Add("@FolderAccountId", SqlDbType.Int).Value = folderAccountId;
        var folders = new List<FolderRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            folders.Add(ReadFolderRow(reader));
        }

        return folders;
    }

    private static string BuildMailboxName(
        FolderRow folder,
        IReadOnlyDictionary<int, FolderRow> foldersById,
        string prefix,
        string hierarchyDelimiter)
    {
        var segments = new Stack<string>();
        var current = folder;
        for (var depth = 0; depth < 250; depth++)
        {
            segments.Push(current.Name);
            if (current.ParentFolderId < 0 || !foldersById.TryGetValue(current.ParentFolderId, out var parent))
            {
                break;
            }

            current = parent;
        }

        var name = string.Join(hierarchyDelimiter, segments);
        return string.IsNullOrEmpty(prefix) ? name : prefix + hierarchyDelimiter + name;
    }

    private static bool WildcardMatch(string mailboxName, string wildcard, string hierarchyDelimiter)
    {
        var regex = new StringBuilder("^");
        foreach (var character in wildcard)
        {
            switch (character)
            {
                case '*':
                    regex.Append(".*");
                    break;

                case '%':
                    regex.Append(hierarchyDelimiter.Length == 1
                        ? "[^" + Regex.Escape(hierarchyDelimiter) + "]*"
                        : ".*");
                    break;

                default:
                    regex.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        regex.Append('$');
        return Regex.IsMatch(
            mailboxName,
            regex.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private async ValueTask<FolderRow?> ResolveFolderPathAsync(
        SqlConnection connection,
        int folderAccountId,
        IReadOnlyList<string> segments,
        CancellationToken cancellationToken)
    {
        FolderRow? folder = null;
        var parentFolderId = -1;

        foreach (var segment in segments)
        {
            folder = await FindChildFolderAsync(
                connection,
                folderAccountId,
                parentFolderId,
                segment,
                cancellationToken).ConfigureAwait(false);
            if (folder is null)
            {
                return null;
            }

            parentFolderId = folder.FolderId;
        }

        return folder;
    }

    private static async ValueTask<FolderRow?> FindChildFolderAsync(
        SqlConnection connection,
        int folderAccountId,
        int parentFolderId,
        string folderName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(FindChildFolderSql, connection);
        command.Parameters.Add("@FolderAccountId", SqlDbType.Int).Value = folderAccountId;
        command.Parameters.Add("@ParentFolderId", SqlDbType.Int).Value = parentFolderId;
        command.Parameters.Add("@FolderName", SqlDbType.NVarChar, 255).Value = folderName;
        return await ReadSingleFolderAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<FolderRow?> LoadFolderByIdAsync(
        SqlConnection connection,
        int folderAccountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectFolderByIdSql, connection);
        command.Parameters.Add("@FolderAccountId", SqlDbType.Int).Value = folderAccountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;
        return await ReadSingleFolderAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<FolderRow?> ReadSingleFolderAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadFolderRow(reader);
    }

    private static FolderRow ReadFolderRow(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetByte(4) == 1,
            reader.GetDateTime(5),
            reader.GetInt64(6));

    private async ValueTask<MailboxAccess> ResolveAccessAsync(
        SqlConnection connection,
        int requesterAccountId,
        FolderRow folder,
        bool isPublicFolder,
        CancellationToken cancellationToken)
    {
        if (!isPublicFolder || !_options.UseAcl)
        {
            return MailboxAccess.Full;
        }

        var current = folder;
        for (var depth = 0; depth < 250 && current is not null; depth++)
        {
            var permissions = await LoadAclPermissionsAsync(connection, current.FolderId, cancellationToken).ConfigureAwait(false);
            if (permissions.Count > 0)
            {
                var permission = await MatchPermissionAsync(
                    connection,
                    permissions,
                    requesterAccountId,
                    cancellationToken).ConfigureAwait(false);
                return permission is null
                    ? MailboxAccess.None
                    : MailboxAccess.FromAclValue(permission.Value);
            }

            if (current.ParentFolderId < 0)
            {
                break;
            }

            current = await LoadFolderByIdAsync(
                connection,
                folderAccountId: 0,
                current.ParentFolderId,
                cancellationToken).ConfigureAwait(false);
        }

        return MailboxAccess.None;
    }

    private static async ValueTask<IReadOnlyList<AclPermissionRow>> LoadAclPermissionsAsync(
        SqlConnection connection,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectAclPermissionsSql, connection);
        command.Parameters.Add("@FolderId", SqlDbType.BigInt).Value = folderId;
        var permissions = new List<AclPermissionRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(new AclPermissionRow(
                reader.GetInt64(0),
                reader.GetByte(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4)));
        }

        return permissions;
    }

    private static async ValueTask<AclPermissionRow?> MatchPermissionAsync(
        SqlConnection connection,
        IReadOnlyList<AclPermissionRow> permissions,
        int requesterAccountId,
        CancellationToken cancellationToken)
    {
        var direct = permissions.FirstOrDefault(permission =>
            permission.PermissionType == 0 && permission.AccountId == requesterAccountId);
        if (direct is not null)
        {
            return direct;
        }

        foreach (var permission in permissions.Where(static candidate => candidate.PermissionType == 1))
        {
            if (await IsGroupMemberAsync(connection, permission.GroupId, requesterAccountId, cancellationToken).ConfigureAwait(false))
            {
                return permission;
            }
        }

        return permissions.FirstOrDefault(static permission => permission.PermissionType == 2);
    }

    private static async ValueTask<bool> IsGroupMemberAsync(
        SqlConnection connection,
        long groupId,
        int requesterAccountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(IsGroupMemberSql, connection);
        command.Parameters.Add("@GroupId", SqlDbType.BigInt).Value = groupId;
        command.Parameters.Add("@AccountId", SqlDbType.BigInt).Value = requesterAccountId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static async ValueTask<MailboxCounters> LoadCountersAsync(
        SqlConnection connection,
        int messageAccountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectMailboxCountersSql, connection);
        command.Parameters.Add("@MessageAccountId", SqlDbType.Int).Value = messageAccountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new MailboxCounters(0, 0, null, 0);
        }

        return new MailboxCounters(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2),
            reader.GetInt64(3));
    }

    private sealed record FolderRow(
        int FolderId,
        int FolderAccountId,
        int ParentFolderId,
        string Name,
        bool IsSubscribed,
        DateTime CreationTime,
        long CurrentUid);

    private sealed record AclPermissionRow(
        long Id,
        byte PermissionType,
        long GroupId,
        long AccountId,
        long Value);

    private sealed record AclPrincipal(
        byte PermissionType,
        long GroupId,
        long AccountId);

    private sealed record AclMailboxContext(
        ImapAclCommandStatus Status,
        string MailboxName,
        FolderRow? Folder,
        MailboxAccess? Access)
    {
        public static AclMailboxContext Failure(ImapAclCommandStatus status) =>
            new(status, string.Empty, null, null);
    }

    private sealed record MailboxCounters(
        long Exists,
        long Recent,
        long? FirstUnseenUid,
        long Unseen);

    private sealed record MailboxAccess(
        bool CanLookup,
        bool CanRead,
        bool CanWrite,
        bool CanAdminister,
        long Value)
    {
        public static readonly MailboxAccess None = new(false, false, false, false, 0);

        public static readonly MailboxAccess Full = new(true, true, true, true, ImapAclRights.All);

        public static MailboxAccess FromAclValue(long value)
        {
            var canLookup = (value & AclLookup) == AclLookup;
            var canRead = (value & AclRead) == AclRead;
            var canWrite = (value & (AclWriteSeen | AclWriteOthers | AclInsert | AclWriteDeleted | AclExpunge)) != 0;
            var canAdminister = (value & AclAdminister) == AclAdminister;
            return new MailboxAccess(canLookup, canRead, canWrite, canAdminister, value);
        }
    }
}
