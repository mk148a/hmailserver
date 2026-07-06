using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class StoreBackedMessageIdResolverTests
{
    private const string DataDirectory = @"C:\hMailServer\Data";

    [TestMethod]
    public async Task RetrieveMessageId_UsesLegacyAccountPartialFilenameBeforeExactPath()
    {
        const long largeMessageId = 0x1_0000_0001;
        const string storedFileName = "{A1B23456-CDEF-7890-ABCD-EF1234567890}.eml";
        var lookup = new RecordingMessageFileNameLookup(
            new Dictionary<string, long> { [storedFileName] = largeMessageId });
        var resolver = new StoreBackedMessageIdResolver(lookup, DataDirectory + @"\");

        var result = await resolver.RetrieveMessageIdAsync(
            $@"c:\HMAILSERVER\data\example.test\user\A1\{storedFileName}",
            CancellationToken.None);

        Assert.AreEqual(largeMessageId, result);
        CollectionAssert.AreEqual(new[] { storedFileName }, lookup.RequestedFileNames);
    }

    [TestMethod]
    public async Task RetrieveMessageId_FallsBackToExactSuppliedPathAfterPartialMiss()
    {
        const string fullPath = @"C:\hMailServer\Data\one.eml";
        var lookup = new RecordingMessageFileNameLookup(
            new Dictionary<string, long> { [fullPath] = 42 });
        var resolver = new StoreBackedMessageIdResolver(lookup, DataDirectory);

        var result = await resolver.RetrieveMessageIdAsync(fullPath, CancellationToken.None);

        Assert.AreEqual(42, result);
        CollectionAssert.AreEqual(new[] { "one.eml", fullPath }, lookup.RequestedFileNames);
    }

    [TestMethod]
    public async Task RetrieveMessageId_UsesQueueFilenameRelativeToDataRoot()
    {
        var lookup = new RecordingMessageFileNameLookup(
            new Dictionary<string, long> { ["queued.eml"] = 7 });
        var resolver = new StoreBackedMessageIdResolver(lookup, DataDirectory);

        var result = await resolver.RetrieveMessageIdAsync(
            DataDirectory + @"\queued.eml",
            CancellationToken.None);

        Assert.AreEqual(7, result);
        CollectionAssert.AreEqual(new[] { "queued.eml" }, lookup.RequestedFileNames);
    }

    [TestMethod]
    public async Task RetrieveMessageId_InvalidAccountBucketUsesExactPathOnly()
    {
        const string fullPath =
            @"C:\hMailServer\Data\example.test\user\B2\{A1B23456-CDEF-7890-ABCD-EF1234567890}.eml";
        var lookup = new RecordingMessageFileNameLookup(
            new Dictionary<string, long> { [fullPath] = 8 });
        var resolver = new StoreBackedMessageIdResolver(lookup, DataDirectory);

        var result = await resolver.RetrieveMessageIdAsync(fullPath, CancellationToken.None);

        Assert.AreEqual(8, result);
        CollectionAssert.AreEqual(new[] { fullPath }, lookup.RequestedFileNames);
    }

    [TestMethod]
    public async Task RetrieveMessageId_PublicFolderPathPreservesLegacyExactFallback()
    {
        const string fullPath =
            @"C:\hMailServer\Data\#Public\A1\{A1B23456-CDEF-7890-ABCD-EF1234567890}.eml";
        var lookup = new RecordingMessageFileNameLookup(
            new Dictionary<string, long> { [fullPath] = 9 });
        var resolver = new StoreBackedMessageIdResolver(lookup, DataDirectory);

        var result = await resolver.RetrieveMessageIdAsync(fullPath, CancellationToken.None);

        Assert.AreEqual(9, result);
        CollectionAssert.AreEqual(new[] { fullPath }, lookup.RequestedFileNames);
    }

    [TestMethod]
    public async Task RetrieveMessageId_ReturnsZeroWhenNoStoredFilenameMatches()
    {
        var lookup = new RecordingMessageFileNameLookup(new Dictionary<string, long>());
        var resolver = new StoreBackedMessageIdResolver(lookup, DataDirectory);

        var result = await resolver.RetrieveMessageIdAsync("missing.eml", CancellationToken.None);

        Assert.AreEqual(0, result);
        CollectionAssert.AreEqual(new[] { "missing.eml" }, lookup.RequestedFileNames);
    }

    private sealed class RecordingMessageFileNameLookup(
        IReadOnlyDictionary<string, long> messageIds) : IMessageFileNameLookup
    {
        public List<string> RequestedFileNames { get; } = [];

        public ValueTask<string> GetFileNameByMessageIdAsync(
            long messageId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(string.Empty);

        public ValueTask<long?> GetMessageIdByFileNameAsync(
            string fileName,
            CancellationToken cancellationToken)
        {
            RequestedFileNames.Add(fileName);
            return ValueTask.FromResult(
                messageIds.TryGetValue(fileName, out var messageId)
                    ? (long?)messageId
                    : null);
        }
    }
}
