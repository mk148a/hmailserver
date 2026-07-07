using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class StoreBackedImportMessageFromFileRuntimeTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 7, 10, 11, 12, TimeSpan.Zero);

    [TestMethod]
    public async Task ImportQueueMessage_UsesLocalToAndCcRecipientsThenSignalsAfterPersistence()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.Write(
            "queue.eml",
            """
            Received: from mx.example; Wed, 01 Jul 2026 03:04:05 +0200
            From: Sender <sender@remote.test>
            To: Local <local@example.test>, Remote <remote@remote.test>
            Cc: Alias <alias@example.test>
            Date: Tue, 30 Jun 2026 12:00:00 +0000
            Subject: Imported

            Body
            """);
        var store = new RecordingStore();
        var validator = new RecordingRecipientValidator();
        var wakeSignal = new RecordingWakeSignal(() => Assert.AreEqual(1, store.QueueImports.Count));
        var runtime = CreateRuntime(directory.Path, store, validator, wakeSignal);

        var result = await runtime.ImportMessageFromFileAsync(
            sourcePath,
            accountId: 0,
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, store.FindCalls.Count);
        Assert.AreEqual("queue.eml", store.FindCalls[0].PartialFileName);
        Assert.AreEqual(sourcePath, store.FindCalls[0].FullFileName);
        Assert.AreEqual(1, store.QueueImports.Count);
        var imported = store.QueueImports[0];
        Assert.AreEqual("queue.eml", imported.FileName);
        Assert.AreEqual("sender@remote.test", imported.FromAddress);
        Assert.AreEqual(new DateTimeOffset(2026, 7, 1, 1, 4, 5, TimeSpan.Zero), imported.CreatedUtc);
        Assert.AreEqual(new FileInfo(sourcePath).Length, imported.Size);
        Assert.AreEqual(1, imported.Recipients.Count);
        Assert.AreEqual("local@example.test", imported.Recipients[0].Address);
        Assert.AreEqual("local@example.test", imported.Recipients[0].OriginalAddress);
        Assert.AreEqual(10, imported.Recipients[0].LocalAccountId);
        Assert.IsTrue(imported.Recipients[0].IsLocal);
        Assert.IsFalse(imported.Recipients[0].IsRouteRecipient);
        Assert.AreEqual(0, store.FolderLookups.Count);
        Assert.AreEqual(1, wakeSignal.SignalCount);
        CollectionAssert.AreEqual(
            new[] { "local@example.test", "remote@remote.test", "alias@example.test" },
            validator.Requests.Select(static request => request.RecipientAddress).ToArray());
        Assert.IsTrue(validator.Requests.All(static request => request.SenderAuthenticated));
        Assert.IsTrue(validator.Requests.All(static request => request.BypassDistributionListAuthorization));
    }

    [TestMethod]
    public async Task ImportAccountMessage_MovesMisplacedFileToLegacyGuidBucketAndPersistsInboxShape()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.Write(
            Path.Combine("example.test", "user", "loose.eml"),
            """
            From: Sender <sender@example.test>
            To: user@example.test
            Date: Thu, 02 Jul 2026 03:04:05 +0000
            Subject: Imported

            Body
            """);
        var store = new RecordingStore
        {
            FolderIds =
            {
                ["42|Inbox"] = 100
            }
        };
        var runtime = CreateRuntime(
            directory.Path,
            store,
            new RecordingRecipientValidator(),
            new RecordingWakeSignal());

        var result = await runtime.ImportMessageFromFileAsync(
            sourcePath,
            accountId: 42,
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, store.DeliveredImports.Count);
        Assert.AreEqual(1, store.FolderLookups.Count);
        CollectionAssert.AreEqual(new[] { "Inbox" }, store.FolderLookups[0].FolderPath);
        Assert.IsFalse(store.FolderLookups[0].CreateMissing);
        var imported = store.DeliveredImports[0];
        Assert.AreEqual(42, imported.AccountId);
        Assert.AreEqual(42, imported.FolderAccountId);
        Assert.AreEqual(100L, imported.FolderId);
        Assert.AreEqual("sender@example.test", imported.FromAddress);
        Assert.AreEqual(new DateTimeOffset(2026, 7, 2, 3, 4, 5, TimeSpan.Zero), imported.CreatedUtc);
        StringAssert.Matches(imported.FileName, new System.Text.RegularExpressions.Regex(
            "^\\{[0-9A-F-]{36}\\}\\.eml$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant));
        var movedPath = Path.Combine(
            directory.Path,
            "example.test",
            "user",
            imported.FileName.Substring(1, 2),
            imported.FileName);
        Assert.IsFalse(File.Exists(sourcePath));
        Assert.IsTrue(File.Exists(movedPath));
        Assert.AreEqual(new FileInfo(movedPath).Length, imported.Size);
    }

    [TestMethod]
    public async Task ImportAccountMessage_ReturnsFalseInsteadOfCreatingMissingInbox()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.Write(
            Path.Combine("example.test", "user", "missing-inbox.eml"),
            ValidMessage("local@example.test"));
        var store = new RecordingStore();
        var runtime = CreateRuntime(
            directory.Path,
            store,
            new RecordingRecipientValidator(),
            new RecordingWakeSignal());

        Assert.IsFalse(await runtime.ImportMessageFromFileAsync(
            sourcePath,
            accountId: 42,
            CancellationToken.None));
        Assert.AreEqual(1, store.FolderLookups.Count);
        Assert.IsFalse(store.FolderLookups[0].CreateMissing);
        Assert.AreEqual(0, store.DeliveredImports.Count);
    }

    [TestMethod]
    public async Task ImportExistingMessage_ReturnsForPartialOrNormalizesFullPathWithoutParsing()
    {
        using var directory = new TemporaryDirectory();
        var partialPath = directory.Write("already.eml", "not mime");
        var partialStore = new RecordingStore
        {
            ExistingReference = new ImportedMessageReference(11, IsPartialFileName: true)
        };
        var partialRuntime = CreateRuntime(
            directory.Path,
            partialStore,
            new RecordingRecipientValidator(),
            new RecordingWakeSignal());

        Assert.IsTrue(await partialRuntime.ImportMessageFromFileAsync(
            partialPath,
            accountId: 0,
            CancellationToken.None));
        Assert.AreEqual(0, partialStore.UpdateCalls.Count);
        Assert.AreEqual(0, partialStore.QueueImports.Count);

        const string legacyFileName = "{A1234567-89AB-CDEF-0123-456789ABCDEF}.eml";
        var fullPath = directory.Write(
            Path.Combine("example.test", "user", "A1", legacyFileName),
            "not mime");
        var fullStore = new RecordingStore
        {
            ExistingReference = new ImportedMessageReference(12, IsPartialFileName: false)
        };
        var fullRuntime = CreateRuntime(
            directory.Path,
            fullStore,
            new RecordingRecipientValidator(),
            new RecordingWakeSignal());

        Assert.IsTrue(await fullRuntime.ImportMessageFromFileAsync(
            fullPath,
            accountId: 42,
            CancellationToken.None));
        Assert.AreEqual((12L, legacyFileName), fullStore.UpdateCalls.Single());
        Assert.IsTrue(File.Exists(fullPath));
    }

    [TestMethod]
    public async Task ImportMessageToImapFolder_UsesNamedFolderPathAndDateMacros()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.Write(
            Path.Combine("example.test", "user", "loose.eml"),
            """
            From: Sender <sender@example.test>
            To: user@example.test
            Date: Thu, 02 Jul 2026 03:04:05 +0000
            Subject: Imported

            Body
            """);
        var store = new RecordingStore
        {
            FolderIds =
            {
                ["42|Inbox|2026"] = 222
            }
        };
        var runtime = CreateRuntime(
            directory.Path,
            store,
            new RecordingRecipientValidator(),
            new RecordingWakeSignal());

        var result = await runtime.ImportMessageFromFileToImapFolderAsync(
            sourcePath,
            accountId: 42,
            imapFolder: ".Inbox.%YEAR%",
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, store.FolderLookups.Count);
        CollectionAssert.AreEqual(new[] { "Inbox", "2026" }, store.FolderLookups[0].FolderPath);
        Assert.IsTrue(store.FolderLookups[0].CreateMissing);
        Assert.AreEqual(222L, store.DeliveredImports.Single().FolderId);
    }

    [TestMethod]
    public async Task ImportMessageToImapFolder_CreatesMissingPrivateAndPublicFoldersAndChecksExistingPublicInsertAcl()
    {
        using var directory = new TemporaryDirectory();
        var privateSource = directory.Write(
            Path.Combine("example.test", "user", "named.eml"),
            ValidMessage("local@example.test"));
        var newPublicSource = directory.Write(
            Path.Combine("example.test", "user", "new-public.eml"),
            ValidMessage("local@example.test"));
        var existingPublicSource = directory.Write(
            Path.Combine("example.test", "user", "existing-public.eml"),
            ValidMessage("local@example.test"));
        var deniedPublicSource = directory.Write(
            Path.Combine("example.test", "user", "denied-public.eml"),
            ValidMessage("local@example.test"));
        var store = new RecordingStore
        {
            FolderIds =
            {
                ["0|Existing"] = 300,
                ["0|Denied"] = 301
            }
        };
        var aclStore = new RecordingAclStore
        {
            RightsByMailbox =
            {
                ["#Public.Existing"] = "li",
                ["#Public.Denied"] = "lr"
            }
        };
        var runtime = CreateRuntime(
            directory.Path,
            store,
            new RecordingRecipientValidator(),
            new RecordingWakeSignal(),
            aclStore: aclStore);

        Assert.IsTrue(await runtime.ImportMessageFromFileToImapFolderAsync(
            privateSource,
            accountId: 42,
            imapFolder: "Inbox.Archive",
            CancellationToken.None));
        Assert.IsTrue(await runtime.ImportMessageFromFileToImapFolderAsync(
            newPublicSource,
            accountId: 42,
            imapFolder: "#Public.New",
            CancellationToken.None));
        Assert.IsTrue(await runtime.ImportMessageFromFileToImapFolderAsync(
            existingPublicSource,
            accountId: 42,
            imapFolder: "#Public.Existing",
            CancellationToken.None));
        Assert.IsFalse(await runtime.ImportMessageFromFileToImapFolderAsync(
            deniedPublicSource,
            accountId: 42,
            imapFolder: "#Public.Denied",
            CancellationToken.None));

        Assert.AreEqual(4, store.FolderLookups.Count);
        CollectionAssert.AreEqual(new[] { "Inbox", "Archive" }, store.FolderLookups[0].FolderPath);
        Assert.AreEqual(42, store.FolderLookups[0].AccountId);
        Assert.IsTrue(store.FolderLookups[0].CreateMissing);
        CollectionAssert.AreEqual(new[] { "New" }, store.FolderLookups[1].FolderPath);
        Assert.AreEqual(0, store.FolderLookups[1].AccountId);
        Assert.AreEqual(3, store.DeliveredImports.Count);
        Assert.AreEqual(42, store.DeliveredImports[0].FolderAccountId);
        Assert.AreEqual(0, store.DeliveredImports[1].FolderAccountId);
        Assert.AreEqual(0, store.DeliveredImports[2].FolderAccountId);
        Assert.AreEqual(42, store.DeliveredImports[2].AccountId);
        CollectionAssert.AreEqual(
            new[] { "#Public.Existing", "#Public.Denied" },
            aclStore.MailboxNames.ToArray());
    }

    [TestMethod]
    public async Task ImportMessageToImapFolder_IgnoresFolderForQueueImport()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.Write("queue.eml", ValidMessage("local@example.test"));
        var store = new RecordingStore();
        var wakeSignal = new RecordingWakeSignal();
        var runtime = CreateRuntime(
            directory.Path,
            store,
            new RecordingRecipientValidator(),
            wakeSignal);

        Assert.IsTrue(await runtime.ImportMessageFromFileToImapFolderAsync(
            sourcePath,
            accountId: 0,
            imapFolder: "#Public.Ignored",
            CancellationToken.None));
        Assert.AreEqual(0, store.FolderLookups.Count);
        Assert.AreEqual(1, store.QueueImports.Count);
        Assert.AreEqual(1, wakeSignal.SignalCount);
    }

    [TestMethod]
    public async Task ImportMessage_ReturnsFalseForOutsidePathMissingLocalRecipientsOrStoreFailure()
    {
        using var directory = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var outsidePath = outside.Write("outside.eml", ValidMessage("outside@remote.test"));
        var runtime = CreateRuntime(
            directory.Path,
            new RecordingStore(),
            new RecordingRecipientValidator(),
            new RecordingWakeSignal());
        Assert.IsFalse(await runtime.ImportMessageFromFileAsync(
            outsidePath,
            accountId: 0,
            CancellationToken.None));

        var noLocalPath = directory.Write("no-local.eml", ValidMessage("outside@remote.test"));
        Assert.IsFalse(await runtime.ImportMessageFromFileAsync(
            noLocalPath,
            accountId: 0,
            CancellationToken.None));

        var failedPath = directory.Write("failed.eml", ValidMessage("local@example.test"));
        var failedStore = new RecordingStore { Exception = new IOException("database failed") };
        var failedSignal = new RecordingWakeSignal();
        var failedRuntime = CreateRuntime(
            directory.Path,
            failedStore,
            new RecordingRecipientValidator(),
            failedSignal);
        Assert.IsFalse(await failedRuntime.ImportMessageFromFileAsync(
            failedPath,
            accountId: 0,
            CancellationToken.None));
        Assert.AreEqual(0, failedSignal.SignalCount);
    }

    private static StoreBackedImportMessageFromFileRuntime CreateRuntime(
        string dataDirectory,
        RecordingStore store,
        RecordingRecipientValidator validator,
        RecordingWakeSignal wakeSignal,
        SqlServerImapMailboxStoreOptions? mailboxOptions = null,
        IImapAclStore? aclStore = null) =>
        new(
            store,
            validator,
            wakeSignal,
            dataDirectory,
            new FixedTimeProvider(FixedNow),
            mailboxOptions,
            aclStore);

    private static string ValidMessage(string recipient) =>
        $"From: sender@example.test\r\nTo: {recipient}\r\nSubject: Test\r\n\r\nBody\r\n";

    private sealed class RecordingStore : IImportMessageFromFileStore
    {
        public ImportedMessageReference? ExistingReference { get; init; }
        public Exception? Exception { get; init; }
        public List<(string? PartialFileName, string FullFileName)> FindCalls { get; } = [];
        public List<(int AccountId, string[] FolderPath, bool CreateMissing)> FolderLookups { get; } = [];
        public Dictionary<string, long> FolderIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(long MessageId, string FileName)> UpdateCalls { get; } = [];
        public List<ImportedDeliveredMessage> DeliveredImports { get; } = [];
        public List<ImportedQueuedMessage> QueueImports { get; } = [];

        public ValueTask<ImportedMessageReference?> FindExistingMessageAsync(
            string? partialFileName,
            string fullFileName,
            CancellationToken cancellationToken)
        {
            FindCalls.Add((partialFileName, fullFileName));
            return ValueTask.FromResult(ExistingReference);
        }

        public ValueTask<bool> UpdateMessageFileNameAsync(
            long messageId,
            string partialFileName,
            CancellationToken cancellationToken)
        {
            UpdateCalls.Add((messageId, partialFileName));
            return ValueTask.FromResult(true);
        }

        public ValueTask<ImportedFolderReference?> ResolveFolderAsync(
            int accountId,
            IReadOnlyList<string> folderPath,
            bool createMissing,
            CancellationToken cancellationToken)
        {
            var segments = folderPath.ToArray();
            FolderLookups.Add((accountId, segments, createMissing));
            var key = accountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                string.Join("|", segments);
            if (FolderIds.TryGetValue(key, out var folderId))
            {
                return ValueTask.FromResult<ImportedFolderReference?>(
                    new ImportedFolderReference(folderId, Existed: true));
            }

            if (!createMissing)
            {
                return ValueTask.FromResult<ImportedFolderReference?>(null);
            }

            folderId = 1000 + FolderIds.Count;
            FolderIds[key] = folderId;
            return ValueTask.FromResult<ImportedFolderReference?>(
                new ImportedFolderReference(folderId, Existed: false));
        }

        public ValueTask ImportDeliveredMessageAsync(
            ImportedDeliveredMessage message,
            CancellationToken cancellationToken)
        {
            ThrowIfConfigured();
            DeliveredImports.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask ImportQueuedMessageAsync(
            ImportedQueuedMessage message,
            CancellationToken cancellationToken)
        {
            ThrowIfConfigured();
            QueueImports.Add(message);
            return ValueTask.CompletedTask;
        }

        private void ThrowIfConfigured()
        {
            if (Exception is not null)
            {
                throw Exception;
            }
        }
    }

    private sealed class RecordingAclStore : IImapAclStore
    {
        public Dictionary<string, string> RightsByMailbox { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> MailboxNames { get; } = [];

        public ValueTask<ImapAclRightsResult> GetMyRightsAsync(
            int requesterAccountId,
            string mailboxName,
            CancellationToken cancellationToken)
        {
            MailboxNames.Add(mailboxName);
            return ValueTask.FromResult(RightsByMailbox.TryGetValue(mailboxName, out var rights)
                ? new ImapAclRightsResult(ImapAclCommandStatus.Success, mailboxName, rights)
                : ImapAclRightsResult.Failure(ImapAclCommandStatus.PermissionDenied));
        }

        public ValueTask<ImapAclListResult> GetAclAsync(
            int requesterAccountId,
            string mailboxName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ImapAclMutationResult> SetAclAsync(
            int requesterAccountId,
            string mailboxName,
            string identifier,
            ImapAclRightsChange rightsChange,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ImapAclMutationResult> DeleteAclAsync(
            int requesterAccountId,
            string mailboxName,
            string identifier,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingRecipientValidator : ISmtpRecipientValidator
    {
        public List<SmtpRecipientValidationRequest> Requests { get; } = [];

        public ValueTask<SmtpRecipientValidationResult> ValidateAsync(
            SmtpRecipientValidationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return request.RecipientAddress switch
            {
                "local@example.test" => ValueTask.FromResult(
                    SmtpRecipientValidationResult.Accept(
                        new SmtpResolvedRecipient(
                            "local@example.test",
                            request.RecipientAddress,
                            10,
                            IsLocal: true))),
                "alias@example.test" => ValueTask.FromResult(
                    SmtpRecipientValidationResult.Accept(
                        new SmtpResolvedRecipient(
                            "local@example.test",
                            request.RecipientAddress,
                            10,
                            IsLocal: true))),
                _ => ValueTask.FromResult(
                    SmtpRecipientValidationResult.Accept(
                        new SmtpResolvedRecipient(
                            request.RecipientAddress,
                            request.RecipientAddress,
                            0,
                            IsLocal: false)))
            };
        }
    }

    private sealed class RecordingWakeSignal(Action? onSignal = null) : IDeliveryQueueWakeSignal
    {
        public int SignalCount { get; private set; }

        public void Signal()
        {
            onSignal?.Invoke();
            SignalCount++;
        }

        public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"hmailserver-net10-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath);
            var parent = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
