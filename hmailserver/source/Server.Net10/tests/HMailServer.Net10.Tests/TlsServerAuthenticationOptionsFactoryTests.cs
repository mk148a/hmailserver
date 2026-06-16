using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class TlsServerAuthenticationOptionsFactoryTests
{
    [TestMethod]
    public void Create_UsesOsTlsPolicyAndRevocationChecks()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=hmailserver.test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var options = TlsServerAuthenticationOptionsFactory.Create(
            certificate,
            requireClientCertificate: true,
            applicationProtocols: new[] { SslApplicationProtocol.Http11 });

        Assert.AreSame(certificate, options.ServerCertificate);
        Assert.IsTrue(options.ClientCertificateRequired);
        Assert.AreEqual(X509RevocationMode.Online, options.CertificateRevocationCheckMode);
        Assert.AreEqual(SslProtocols.None, options.EnabledSslProtocols);
        Assert.AreEqual(SslApplicationProtocol.Http11, options.ApplicationProtocols![0]);
    }
}
