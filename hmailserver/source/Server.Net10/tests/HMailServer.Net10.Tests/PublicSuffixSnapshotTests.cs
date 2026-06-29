using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class PublicSuffixSnapshotTests
{
    [TestMethod]
    public void Snapshot_MetadataMatchesPackagedBytesAndHeaders()
    {
        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "public_suffix_list.dat");
        var metadataPath = Path.Combine(AppContext.BaseDirectory, "public_suffix_list.meta.json");
        Assert.IsTrue(File.Exists(snapshotPath));
        Assert.IsTrue(File.Exists(metadataPath));

        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var snapshotText = Encoding.UTF8.GetString(snapshotBytes);
        var sha256 = Convert.ToHexString(SHA256.HashData(snapshotBytes)).ToLowerInvariant();
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(metadataPath));
        var metadata = metadataDocument.RootElement;

        Assert.AreEqual(
            "https://publicsuffix.org/list/public_suffix_list.dat",
            metadata.GetProperty("sourceUrl").GetString());
        Assert.AreEqual(sha256, metadata.GetProperty("sha256").GetString());
        Assert.AreEqual(snapshotBytes.LongLength, metadata.GetProperty("byteLength").GetInt64());
        StringAssert.Contains(
            snapshotText,
            "// VERSION: " + metadata.GetProperty("upstreamVersion").GetString());
        StringAssert.Contains(
            snapshotText,
            "// COMMIT: " + metadata.GetProperty("upstreamCommit").GetString());
        StringAssert.Contains(snapshotText, "https://mozilla.org/MPL/2.0/");
    }

    [TestMethod]
    public async Task Snapshot_ResolvesCurrentMultiLabelPublicSuffixRules()
    {
        var resolver = new PublicSuffixDmarcOrganizationalDomainResolver(
            Path.Combine(AppContext.BaseDirectory, "public_suffix_list.dat"));

        var organizationalDomain = await resolver.ResolveAsync(
            "mail.example.co.uk",
            CancellationToken.None);

        Assert.AreEqual("example.co.uk", organizationalDomain);
    }
}
