using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DmarcOrganizationalDomainResolverTests
{
    private const string PublicSuffixRules = """
        // ===BEGIN ICANN DOMAINS===
        com
        uk
        co.uk
        jp
        kawasaki.jp
        *.kawasaki.jp
        !city.kawasaki.jp
        // ===END ICANN DOMAINS===
        """;

    [TestMethod]
    public async Task ResolveAsync_ReturnsRegistrableDomainForMultiLabelPublicSuffix()
    {
        var path = await CreateRuleFileAsync();
        try
        {
            var resolver = new PublicSuffixDmarcOrganizationalDomainResolver(path);

            var organizationalDomain = await resolver.ResolveAsync(
                "Mail.Example.Co.Uk.",
                CancellationToken.None);

            Assert.AreEqual("example.co.uk", organizationalDomain);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_HonorsWildcardAndExceptionRules()
    {
        var path = await CreateRuleFileAsync();
        try
        {
            var resolver = new PublicSuffixDmarcOrganizationalDomainResolver(path);

            var wildcardDomain = await resolver.ResolveAsync(
                "foo.bar.kawasaki.jp",
                CancellationToken.None);
            var exceptionDomain = await resolver.ResolveAsync(
                "www.city.kawasaki.jp",
                CancellationToken.None);

            Assert.AreEqual("foo.bar.kawasaki.jp", wildcardDomain);
            Assert.AreEqual("city.kawasaki.jp", exceptionDomain);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_FailsOpenWhenRuleFileCannotBeLoaded()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-missing-psl-" + Guid.NewGuid().ToString("N") + ".dat");
        var resolver = new PublicSuffixDmarcOrganizationalDomainResolver(path);

        var organizationalDomain = await resolver.ResolveAsync(
            "mail.example.com",
            CancellationToken.None);

        Assert.IsNull(organizationalDomain);
    }

    [TestMethod]
    public async Task ResolveAsync_FailsOpenWhenRuleFileIsEmpty()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-empty-psl-" + Guid.NewGuid().ToString("N") + ".dat");
        await File.WriteAllTextAsync(path, string.Empty);
        try
        {
            var resolver = new PublicSuffixDmarcOrganizationalDomainResolver(path);

            var organizationalDomain = await resolver.ResolveAsync(
                "mail.example.com",
                CancellationToken.None);

            Assert.IsNull(organizationalDomain);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_PropagatesCancellation()
    {
        var path = await CreateRuleFileAsync();
        try
        {
            var resolver = new PublicSuffixDmarcOrganizationalDomainResolver(path);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                async () => await resolver.ResolveAsync("mail.example.com", cancellation.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> CreateRuleFileAsync()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-psl-" + Guid.NewGuid().ToString("N") + ".dat");
        await File.WriteAllTextAsync(path, PublicSuffixRules);
        return path;
    }
}
