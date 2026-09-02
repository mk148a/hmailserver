using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageMutationStoreTests
{
    [TestMethod]
    public void PlanStore_UsesUidOrSequenceRanges()
    {
        var plan = SqlServerImapMessageMutationStore.PlanStore(
            new ImapStoreRequest(
                AccountId: 10,
                FolderId: 20,
                MessageSet: [new ImapIdRange(101, null)],
                UseUid: true,
                Mode: ImapStoreMode.Add,
                Flags: ImapMessageFlags.Seen,
                Silent: false));

        StringAssert.Contains(plan.CommandText, "ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)");
        StringAssert.Contains(plan.CommandText, "messageuid >= @RangeStart0");
        Assert.AreEqual(101L, plan.Parameters["@RangeStart0"]);

        var sequencePlan = SqlServerImapMessageMutationStore.PlanStore(
            new ImapStoreRequest(
                AccountId: 10,
                FolderId: 20,
                MessageSet: [new ImapIdRange(1, 10)],
                UseUid: false,
                Mode: ImapStoreMode.Remove,
                Flags: ImapMessageFlags.Deleted,
                Silent: true));

        StringAssert.Contains(sequencePlan.CommandText, "sequencenumber BETWEEN @RangeStart0 AND @RangeEnd0");
    }

    [TestMethod]
    public void ExpungeSql_DeletesMessagesAndSearchArtifacts()
    {
        var sql = SqlServerImapMessageMutationStore.BuildExpungeSnapshotSql();

        StringAssert.Contains(sql, "(messageflags & @DeletedFlag) = @DeletedFlag");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_message_search_queue");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_message_search_documents");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_message_metadata");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_messages");
    }

    [TestMethod]
    public void StoreSql_RevalidatesPublicMailboxAclInsideMutationTransaction()
    {
        var sql = SqlServerImapMessageMutationStore.SelectEffectiveAclValueSql;

        StringAssert.Contains(sql, "WITH FolderChain AS");
        StringAssert.Contains(sql, "UPDLOCK, HOLDLOCK");
        StringAssert.Contains(sql, "@RequesterAccountId");
        StringAssert.Contains(sql, "hm_group_members");
        StringAssert.Contains(ReadStoreSource(), "m WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(ReadStoreSource(), "request.AccountId != 0 || !_useAcl");
        StringAssert.Contains(ReadStoreSource(), "EnsureStoreAuthorizationAsync(connection, transaction, request, rows");
        StringAssert.Contains(ReadStoreSource(), "currentFlags ^ updatedFlags");
        StringAssert.Contains(ReadStoreSource(), "GetRequiredStoreRights(row.Flags, ApplyFlags(row, request))");
        StringAssert.Contains(ReadStoreSource(), "GetRequiredStoreRights(request.Flags)");
        StringAssert.Contains(ReadStoreSource(), "BeginTransactionAsync(cancellationToken)");
    }

    [TestMethod]
    public void ExpungeStore_InjectsOptionalAccountSizeInvalidationCallback()
    {
        var constructor = typeof(SqlServerImapMessageMutationStore).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            new[]
            {
                typeof(SqlServerConnectionFactory),
                typeof(MessageFilePathResolver),
                typeof(Action<int>)
            },
            modifiers: null);

        Assert.IsNotNull(constructor);
    }

    [TestMethod]
    public void ExpungeStore_InvokesCallbackOnceAfterCommitBeforeFileCleanup()
    {
        var source = ReadExpungeCoreSource();
        var commitIndex = source.IndexOf("await transaction.CommitAsync", StringComparison.Ordinal);
        var callbackIndex = source.IndexOf(
            "_accountSizeInvalidationCallback?.Invoke(accountId)",
            StringComparison.Ordinal);
        var cleanupIndex = source.IndexOf("TryDeleteMessageFile(row)", StringComparison.Ordinal);

        Assert.IsTrue(commitIndex >= 0);
        Assert.IsTrue(callbackIndex > commitIndex);
        Assert.IsTrue(cleanupIndex > callbackIndex);
        Assert.AreEqual(
            1,
            CountOccurrences(source, "_accountSizeInvalidationCallback?.Invoke(accountId)"));
        StringAssert.Contains(ReadStoreSource(), "Action<int>? accountSizeInvalidationCallback = null");
    }

    [TestMethod]
    public void ExpungeStore_SkipsCallbackForZeroRowsAndFailedTransactions()
    {
        var source = ReadExpungeCoreSource();
        var zeroRowsIndex = source.IndexOf("if (rows.Count == 0)", StringComparison.Ordinal);
        var rollbackIndex = source.IndexOf(
            "await transaction.RollbackAsync(cancellationToken)",
            StringComparison.Ordinal);
        var callbackIndex = source.IndexOf(
            "_accountSizeInvalidationCallback?.Invoke(accountId)",
            StringComparison.Ordinal);

        Assert.IsTrue(zeroRowsIndex >= 0);
        Assert.IsTrue(rollbackIndex >= 0);
        Assert.IsTrue(zeroRowsIndex < callbackIndex);
        Assert.IsTrue(rollbackIndex < callbackIndex);
    }

    [TestMethod]
    public async Task ExpungeAsync_WhenCanceledBeforeSql_DoesNotInvokeAccountSizeInvalidation()
    {
        var invalidatedAccountIds = new List<int>();
        var store = new SqlServerImapMessageMutationStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            invalidatedAccountIds.Add);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var enumerator = store.ExpungeDeletedAsync(11, 12, cancellationTokenSource.Token).GetAsyncEnumerator();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        CollectionAssert.AreEqual(Array.Empty<int>(), invalidatedAccountIds);
    }

    [TestMethod]
    public async Task ExpungeAsync_AllowsLegacyPublicFolderAccountZero()
    {
        var admission = new RecordingWriterAdmission();
        var store = new SqlServerImapMessageMutationStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            accountSizeInvalidationCallback: null,
            enterWriter: admission.EnterAsync);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var enumerator = store.ExpungeDeletedAsync(0, 12, cancellationTokenSource.Token).GetAsyncEnumerator();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        Assert.IsTrue(admission.WasEntered);
        Assert.IsTrue(admission.WasReleased);
    }

    [TestMethod]
    public async Task ExpungeAsync_HoldsWriterAdmissionAndReleasesItOnCancellation()
    {
        var admission = new RecordingWriterAdmission();
        var store = new SqlServerImapMessageMutationStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            accountSizeInvalidationCallback: null,
            enterWriter: admission.EnterAsync);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var enumerator = store.ExpungeDeletedAsync(11, 12, cancellationTokenSource.Token).GetAsyncEnumerator();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        Assert.IsTrue(admission.WasEntered);
        Assert.IsTrue(admission.WasReleased);
        Assert.IsFalse(admission.IsHeld);
    }

    private sealed class RecordingWriterAdmission
    {
        public bool WasEntered { get; private set; }
        public bool WasReleased { get; private set; }
        public bool IsHeld => WasEntered && !WasReleased;

        public ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
        {
            WasEntered = true;
            return ValueTask.FromResult<IDisposable>(new Lease(this));
        }

        private sealed class Lease(RecordingWriterAdmission owner) : IDisposable
        {
            public void Dispose() => owner.WasReleased = true;
        }
    }

    private static string ReadStoreSource()
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Server.Net10",
            "src",
            "HMailServer.Storage.SqlServer",
            "SqlServerImapMessageMutationStore.cs");
        return File.ReadAllText(Path.GetFullPath(sourcePath));
    }

    private static string ReadExpungeCoreSource()
    {
        var source = ReadStoreSource();
        var startIndex = source.IndexOf(
            "private async ValueTask<IReadOnlyList<ImapExpungedMessage>> ExpungeDeletedCoreAsync",
            StringComparison.Ordinal);
        var endIndex = source.IndexOf(
            "private async ValueTask<IReadOnlyList<MessageMutationRow>> LoadDeletedRowsAsync",
            startIndex,
            StringComparison.Ordinal);

        Assert.IsTrue(startIndex >= 0);
        Assert.IsTrue(endIndex > startIndex);
        return source[startIndex..endIndex];
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }
}
