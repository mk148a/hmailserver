using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SetAdministratorPasswordComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);

    [TestMethod]
    public void LegacyPasswordHasher_ProducesVerifierCompatibleSaltedSha256Hash()
    {
        var hash = LegacyPasswordHasher.CreateSaltedSha256("secret");

        Assert.AreEqual(70, hash.Length);
        StringAssert.Matches(hash, new System.Text.RegularExpressions.Regex("^[0-9a-f]{70}$"));

        var provider = new LegacyServerAdministratorAuthenticationProvider(hash);
        Assert.IsTrue(provider.Authenticate("Administrator", "secret"));
        Assert.IsFalse(provider.Authenticate("Administrator", "wrong"));
    }

    [TestMethod]
    public void LiveVerifier_PublishesNewHashAndStopsAcceptingOldPassword()
    {
        var oldHash = LegacyPasswordHasher.CreateSaltedSha256("old-secret");
        var provider = new LegacyServerAdministratorAuthenticationProvider(oldHash);
        var newHash = LegacyPasswordHasher.CreateSaltedSha256("new-secret");

        provider.PublishStoredPasswordHash(newHash);

        Assert.IsFalse(provider.Authenticate("Administrator", "old-secret"));
        Assert.IsTrue(provider.Authenticate("Administrator", "new-secret"));
    }

    [TestMethod]
    public void AdministratorPasswordWriter_PersistsHashBeforePublishingLiveVerifier()
    {
        var path = CreateTemporaryInitializationFile("[Settings]\nUseLanguage=English\n");
        var provider = new LegacyServerAdministratorAuthenticationProvider(
            LegacyPasswordHasher.CreateSaltedSha256("old-secret"));
        TrackingAuthorizationLease? lease = null;
        var settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: static () => true,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                AdministratorPasswordWriter: password =>
                {
                    var hash = LegacyPasswordHasher.CreateSaltedSha256(password);
                    Assert.IsTrue(LegacyInitializationFile.SaveAdministratorPasswordHash(path, hash));
                    Assert.IsFalse(lease!.Disposed);
                    provider.PublishStoredPasswordHash(hash);
                }),
            authorizationLeaseFactory: _ =>
            {
                lease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        try
        {
            settings.SetAdministratorPassword("new-secret");

            var persistedHash = LegacyInitializationFile.LoadAdministratorPasswordHash(path);
            Assert.AreEqual(70, persistedHash.Length);
            Assert.IsTrue(provider.Authenticate("Administrator", "new-secret"));
            Assert.IsFalse(provider.Authenticate("Administrator", "old-secret"));
            Assert.IsTrue(lease!.Disposed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void AdministratorPasswordPersistenceFailure_DoesNotPublishLiveVerifier()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(missingDirectory, "hMailServer.ini");
        var provider = new LegacyServerAdministratorAuthenticationProvider(
            LegacyPasswordHasher.CreateSaltedSha256("old-secret"));
        var settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: static () => true,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                AdministratorPasswordWriter: password =>
                {
                    var hash = LegacyPasswordHasher.CreateSaltedSha256(password);
                    if (!LegacyInitializationFile.SaveAdministratorPasswordHash(path, hash))
                    {
                        throw new IOException("write failed");
                    }

                    provider.PublishStoredPasswordHash(hash);
                }));

        try
        {
            var error = Assert.ThrowsExactly<COMException>(
                () => settings.SetAdministratorPassword("new-secret"));

            Assert.AreEqual(EFail, error.ErrorCode);
            Assert.IsTrue(provider.Authenticate("Administrator", "old-secret"));
            Assert.IsFalse(provider.Authenticate("Administrator", "new-secret"));
        }
        finally
        {
            if (Directory.Exists(missingDirectory))
            {
                Directory.Delete(missingDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void RetainedSettings_StopsUsingPasswordWriterAfterAuthorizationIsRevoked()
    {
        var isAdministrator = true;
        var writeCount = 0;
        var settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: () => isAdministrator,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                AdministratorPasswordWriter: _ => writeCount++));

        settings.SetAdministratorPassword("new-secret");
        isAdministrator = false;

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.SetAdministratorPassword("another-secret"));

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(1, writeCount);
    }

    [TestMethod]
    public void DirectSettingsActivation_DeniesAdministratorPasswordChange()
    {
        var error = Assert.ThrowsExactly<COMException>(
            () => new Settings().SetAdministratorPassword("new-secret"));

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
    }

    [TestMethod]
    public void Interface_PreservesSetAdministratorPasswordDispatchContract()
    {
        var method = typeof(IInterfaceSettings).GetMethod(nameof(IInterfaceSettings.SetAdministratorPassword));

        Assert.IsNotNull(method);
        Assert.AreEqual(76, method!.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(string), method.GetParameters()[0].ParameterType);
        Assert.AreEqual(UnmanagedType.BStr, method.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static string CreateTemporaryInitializationFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }

    private sealed class TrackingAuthorizationLease : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
