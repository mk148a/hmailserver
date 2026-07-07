using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessageFilePathResolverTests
{
    [TestMethod]
    public void Resolve_UsesAccountGuidBucketForDeliveredMessages()
    {
        var options = new MessageFileSearchDocumentSourceOptions(Path.Combine(Path.GetTempPath(), "hmail-data"));
        var resolver = new MessageFilePathResolver(options);

        var resolved = resolver.Resolve(
            "abcdef.eml",
            accountId: 10,
            folderId: 20,
            accountAddress: "user@example.test");

        Assert.AreEqual(
            Path.Combine(options.NormalizedDataDirectory, "example.test", "user", "ab", "abcdef.eml"),
            resolved);
    }

    [TestMethod]
    public void Resolve_UsesDataRootForQueuedSmtpMessages()
    {
        var options = new MessageFileSearchDocumentSourceOptions(Path.Combine(Path.GetTempPath(), "hmail-data"));
        var resolver = new MessageFilePathResolver(options);

        var resolved = resolver.Resolve(
            "abcdef.eml",
            accountId: 0,
            folderId: 0,
            accountAddress: null);

        Assert.AreEqual(
            Path.Combine(options.NormalizedDataDirectory, "abcdef.eml"),
            resolved);
    }

    [TestMethod]
    public void Resolve_UsesLegacyGuidCharactersForBracedMessageFileBucket()
    {
        var options = new MessageFileSearchDocumentSourceOptions(Path.Combine(Path.GetTempPath(), "hmail-data"));
        var resolver = new MessageFilePathResolver(options);
        const string fileName = "{A1234567-89AB-CDEF-0123-456789ABCDEF}.eml";

        var resolved = resolver.Resolve(
            fileName,
            accountId: 10,
            folderId: 20,
            accountAddress: "user@example.test");

        Assert.AreEqual(
            Path.Combine(options.NormalizedDataDirectory, "example.test", "user", "A1", fileName),
            resolved);
    }

    [TestMethod]
    public void Resolve_RejectsTraversalOutsideDataDirectory()
    {
        var options = new MessageFileSearchDocumentSourceOptions(Path.Combine(Path.GetTempPath(), "hmail-data"));
        var resolver = new MessageFilePathResolver(options);

        var resolved = resolver.Resolve(
            "..\\outside.eml",
            accountId: 0,
            folderId: 0,
            accountAddress: null);

        Assert.IsNull(resolved);
    }
}
