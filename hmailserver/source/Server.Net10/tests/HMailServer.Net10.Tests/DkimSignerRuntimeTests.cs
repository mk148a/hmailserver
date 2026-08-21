using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DkimSignerRuntimeTests
{
    [TestMethod]
    public async Task SignAsync_SignsFirstAttemptAndExistingVerifierAcceptsIt()
    {
        using var fixture = new SignerFixture();
        var signed = await fixture.Signer.SignAsync(fixture.Message, fixture.MessageBytes, CancellationToken.None);

        Assert.IsNotNull(signed);
        var signedText = Encoding.Latin1.GetString(signed);
        var separator = signedText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        Assert.IsTrue(separator > 0);
        var originalHeaderStart = signedText.IndexOf("\r\nFrom:", StringComparison.Ordinal);
        if (originalHeaderStart >= 0)
        {
            originalHeaderStart += 2;
        }

        Assert.IsTrue(originalHeaderStart > 0);
        var signatureHeader = signedText[..(originalHeaderStart - 2)];
        var headerBlock = signedText[originalHeaderStart..separator];
        var body = signedText[(separator + 4)..];
        var unfoldedSignatureHeader = signatureHeader.Replace("\r\n\t", " ", StringComparison.Ordinal);
        Assert.IsTrue(DkimSignatureParser.TryParse(unfoldedSignatureHeader, out var signature, out var error), $"{error}; header={unfoldedSignatureHeader}");
        Assert.IsNotNull(signature);

        var evaluation = DkimSignatureVerifier.Verify(
            headerBlock,
            body,
            unfoldedSignatureHeader,
            signature!,
            fixture.PublicKeyBase64);

        Assert.AreEqual(DkimResult.Pass, evaluation.Result, evaluation.Diagnostic);
    }

    [TestMethod]
    public async Task SignAsync_RejectsKeyOutsideConfiguredDataDirectory()
    {
        using var fixture = new SignerFixture(configuredKeyPath: Path.Combine(Path.GetTempPath(), "outside-dkim-key.pem"));

        var signed = await fixture.Signer.SignAsync(fixture.Message, fixture.MessageBytes, CancellationToken.None);

        Assert.IsNull(signed);
    }

    [TestMethod]
    public async Task SignAsync_RejectsTraversalAndHeaderInjectionSelector()
    {
        using var traversal = new SignerFixture(configuredKeyPath: "..\\outside.pem");
        Assert.IsNull(await traversal.Signer.SignAsync(traversal.Message, traversal.MessageBytes, CancellationToken.None));

        using var injected = new SignerFixture(selector: "s1\r\nX-Injected: yes");
        Assert.IsNull(await injected.Signer.SignAsync(injected.Message, injected.MessageBytes, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_NoOpsForExistingSameDomainSignatureOrRetry()
    {
        using var fixture = new SignerFixture();
        var existing = Encoding.Latin1.GetBytes(
            "DKIM-Signature: v=1; a=rsa-sha256; d=example.test; s=old; h=from; bh=abc; b=def\r\n" +
            "From: sender@example.test\r\n\r\nBody\r\n");

        Assert.IsNull(await fixture.Signer.SignAsync(fixture.Message, existing, CancellationToken.None));

        var retryMessage = fixture.Message with { CurrentRetryCount = 1 };
        Assert.IsNull(await fixture.Signer.SignAsync(retryMessage, fixture.MessageBytes, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_NoOpsWhenSigningIsDisabledOrKeyIsMissing()
    {
        using var disabled = new SignerFixture(signEnabled: false);
        Assert.IsNull(await disabled.Signer.SignAsync(disabled.Message, disabled.MessageBytes, CancellationToken.None));

        using var missing = new SignerFixture(configuredKeyPath: "missing.pem");
        Assert.IsNull(await missing.Signer.SignAsync(missing.Message, missing.MessageBytes, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_NoOpsForInvalidOrOversizedPrivateKey()
    {
        using var invalid = new SignerFixture(keyContents: "not a PEM private key");
        Assert.IsNull(await invalid.Signer.SignAsync(invalid.Message, invalid.MessageBytes, CancellationToken.None));

        using var oversized = new SignerFixture(keyContents: new string('x', DkimSignerRuntime.MaxPrivateKeyBytes + 1));
        Assert.IsNull(await oversized.Signer.SignAsync(oversized.Message, oversized.MessageBytes, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_RejectsFinalReparsePointKey()
    {
        using var fixture = new SignerFixture();
        try
        {
            fixture.ReplaceKeyWithFinalReparsePoint();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive("The test host does not allow creating a disposable key reparse point: " + exception.Message);
        }

        var openMethod = typeof(DkimSignerRuntime).GetMethod(
            "OpenPrivateKeyStream",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(openMethod);
        using var opened = (FileStream?)openMethod.Invoke(null, [fixture.ConfiguredKeyPath]);
        Assert.IsNull(opened);
        Assert.IsNull(await fixture.Signer.SignAsync(fixture.Message, fixture.MessageBytes, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignAsync_PreservesLegacyTenMegabyteNoSignBoundary()
    {
        using var fixture = new SignerFixture();
        var oversizedMessage = Encoding.Latin1.GetBytes(
            "From: sender@example.test\r\nSubject: Test\r\n\r\n"
            + new string('x', DkimSignerRuntime.LegacyMaximumMessageBytes));

        var signed = await fixture.Signer.SignAsync(fixture.Message, oversizedMessage, CancellationToken.None);

        Assert.IsNull(signed);
    }

    private sealed class SignerFixture : IDisposable
    {
        private readonly string _root;
        private readonly RSA _rsa;

        public SignerFixture(
            string selector = "s1",
            string? configuredKeyPath = null,
            bool signEnabled = true,
            string? keyContents = null)
        {
            _root = Path.Combine(Path.GetTempPath(), "hmailserver-dkim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            var keyPath = configuredKeyPath ?? Path.Combine("keys", "example.pem");
            if (configuredKeyPath is null)
            {
                var fullKeyPath = Path.Combine(_root, keyPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullKeyPath)!);
                _rsa = RSA.Create(2048);
                File.WriteAllText(fullKeyPath, keyContents ?? _rsa.ExportPkcs8PrivateKeyPem(), Encoding.ASCII);
                PublicKeyBase64 = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
            }
            else
            {
                _rsa = RSA.Create(2048);
                PublicKeyBase64 = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
            }

            var domain = new DomainAdministrationSnapshot(
                Id: 10,
                Name: "example.test",
                Active: true,
                DkimSignEnabled: signEnabled,
                DkimSelector: selector,
                DkimPrivateKeyFile: keyPath,
                DkimHeaderCanonicalizationMethod: 2,
                DkimBodyCanonicalizationMethod: 2,
                DkimSigningAlgorithm: 2);
            Signer = new DkimSignerRuntime(
                _root,
                new FixedDomainStore(domain),
                new FixedAliasStore());
            Message = new DeliveryQueuedMessage(
                new MessageIdentity(10, 0, 0, 0),
                "queue.eml",
                "sender@example.test",
                64,
                DateTimeOffset.UtcNow,
                0,
                0,
                [new DeliveryQueueRecipient(1, "user@example.test", "user@example.test", 42)]);
            MessageBytes = Encoding.Latin1.GetBytes(
                "From: sender@example.test\r\nSubject: Test\r\nDate: Thu, 01 Jan 2026 00:00:00 +0000\r\n\r\nBody\r\n");
        }

        public DkimSignerRuntime Signer { get; }
        public DeliveryQueuedMessage Message { get; }
        public byte[] MessageBytes { get; }
        public string PublicKeyBase64 { get; }
        public string ConfiguredKeyPath => Path.Combine(_root, "keys", "example.pem");

        public void ReplaceKeyWithFinalReparsePoint()
        {
            var keyPath = ConfiguredKeyPath;
            var targetPath = Path.Combine(_root, "real.pem");
            File.Move(keyPath, targetPath);
            File.CreateSymbolicLink(keyPath, targetPath);
        }

        public void Dispose()
        {
            _rsa.Dispose();
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FixedDomainStore(DomainAdministrationSnapshot domain) : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>([domain]);
    }

    private sealed class FixedAliasStore : IDomainAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAliasAdministrationSnapshot>>([]);
    }
}
