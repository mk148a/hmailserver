using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ComLocalServerHostTests
{
    private const uint CoinitMultithreaded = 0;
    private const uint ClsctxLocalServer = 0x4;
    private const int RpcEChangedMode = unchecked((int)0x80010106);
    private const int RegdbEClassNotRegistered = unchecked((int)0x80040154);

    [TestMethod]
    public void RegisteredFactory_ActivatesUtilitiesWithPureHelpersAndAdministrativeBoundary()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(classId, static () => new Utilities()));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceUtilities).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var adapter = (IInterfaceUtilities)Marshal.GetObjectForIUnknown(interfacePointer);
                Assert.AreEqual("dc647eb65e6711e155375218212b3964", adapter.MD5("Password"));

                var denied = Assert.ThrowsExactly<COMException>(
                    () => adapter.MakeDependent("MSSQLSERVER"));
                Assert.AreEqual(unchecked((int)0x80070005), denied.ErrorCode);

                if (Marshal.IsComObject(adapter))
                {
                    Marshal.FinalReleaseComObject(adapter);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [TestMethod]
    public void RegisteredFactory_ActivatesDirectLinksWithLegacyAccessDenied()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(classId, static () => new Links()));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceLinks).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var adapter = (IInterfaceLinks)Marshal.GetObjectForIUnknown(interfacePointer);
                var denied = Assert.ThrowsExactly<COMException>(() => _ = adapter.get_Domain(10));
                Assert.AreEqual(unchecked((int)0x80070005), denied.ErrorCode);

                if (Marshal.IsComObject(adapter))
                {
                    Marshal.FinalReleaseComObject(adapter);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [TestMethod]
    public void RegisteredFactory_ActivatesDirectMessageIndexingWithLegacyAccessDenied()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(classId, static () => new MessageIndexing()));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceMessageIndexing).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var adapter = (IInterfaceMessageIndexing)Marshal.GetObjectForIUnknown(interfacePointer);
                var error = Assert.ThrowsExactly<COMException>(() => _ = adapter.TotalMessageCount);

                Assert.AreEqual(unchecked((int)0x80070005), error.ErrorCode);
                if (Marshal.IsComObject(adapter))
                {
                    Marshal.FinalReleaseComObject(adapter);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }

            host.Dispose();

            var revokedInterfaceId = typeof(IInterfaceMessageIndexing).GUID;
            var revokedActivationResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in revokedInterfaceId,
                out var revokedInterfacePointer);

            Assert.AreEqual(RegdbEClassNotRegistered, revokedActivationResult);
            Assert.AreEqual(nint.Zero, revokedInterfacePointer);
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [TestMethod]
    public void RegisteredFactory_ActivatesApplicationAndAuthenticatesLegacyServerAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        MessageIndexingRuntimeHost.Configure(new TestMessageIndexingRuntime(37));
        DomainAdministrationRuntimeHost.Configure(
            new TestDomainAdministrationStore(
                new[]
                {
                    new DomainAdministrationSnapshot(10, "alpha.example", true)
                }));
        AccountAdministrationRuntimeHost.Configure(
            new TestAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(20, 10, "admin@alpha.example", true, 2)
                }));
        AliasAdministrationRuntimeHost.Configure(
            new TestAliasAdministrationStore(
                new[]
                {
                    new AliasAdministrationSnapshot(30, 10, "abuse@alpha.example", "admin@alpha.example", true)
                }));
        DomainAliasAdministrationRuntimeHost.Configure(
            new TestDomainAliasAdministrationStore(
                new[]
                {
                    new DomainAliasAdministrationSnapshot(35, 10, "alias.alpha.example")
                }));
        DistributionListAdministrationRuntimeHost.Configure(
            new TestDistributionListAdministrationStore(
                new[]
                {
                    new DistributionListAdministrationSnapshot(
                        40,
                        10,
                        "announce@alpha.example",
                        true,
                        false,
                        string.Empty,
                        (int)ComDistributionListMode.Public)
                }));
        DistributionListRecipientAdministrationRuntimeHost.Configure(
            new TestDistributionListRecipientAdministrationStore(
                new[]
                {
                    new DistributionListRecipientAdministrationSnapshot(
                        50,
                        40,
                        "admin@alpha.example")
                }));
        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(
                classId,
                static () => Application.CreateForRuntime(new TestAdministratorAuthenticationProvider("secret"))));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceApplication).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var application = (IInterfaceApplication)Marshal.GetObjectForIUnknown(interfacePointer);

                Assert.IsNull(application.Authenticate("Administrator", "wrong"));
                var account = application.Authenticate("administrator", "secret");
                Assert.IsNotNull(account);
                Assert.AreEqual(ComAdminLevel.ServerAdministrator, account.AdminLevel);
                var settings = application.Settings;
                var messageIndexing = settings.MessageIndexing;
                Assert.AreEqual(37, messageIndexing.TotalMessageCount);
                var domains = application.Domains;
                Assert.AreEqual(1, domains.Count);
                Assert.AreEqual("alpha.example", domains[0].Name);
                var accounts = domains[0].Accounts;
                Assert.AreEqual(1, accounts.Count);
                Assert.AreEqual("admin@alpha.example", accounts[0].Address);
                var aliases = domains[0].Aliases;
                Assert.AreEqual(1, aliases.Count);
                Assert.AreEqual("abuse@alpha.example", aliases[0].Name);
                var domainAliases = domains[0].DomainAliases;
                Assert.AreEqual(1, domainAliases.Count);
                Assert.AreEqual("alias.alpha.example", domainAliases[0].AliasName);
                var distributionLists = domains[0].DistributionLists;
                Assert.AreEqual(1, distributionLists.Count);
                Assert.AreEqual("announce@alpha.example", distributionLists[0].Address);
                var recipients = distributionLists[0].Recipients;
                Assert.AreEqual(1, recipients.Count);
                Assert.AreEqual("admin@alpha.example", recipients[0].RecipientAddress);

                if (Marshal.IsComObject(recipients))
                {
                    Marshal.FinalReleaseComObject(recipients);
                }

                if (Marshal.IsComObject(distributionLists))
                {
                    Marshal.FinalReleaseComObject(distributionLists);
                }

                if (Marshal.IsComObject(domainAliases))
                {
                    Marshal.FinalReleaseComObject(domainAliases);
                }

                if (Marshal.IsComObject(aliases))
                {
                    Marshal.FinalReleaseComObject(aliases);
                }

                if (Marshal.IsComObject(accounts))
                {
                    Marshal.FinalReleaseComObject(accounts);
                }

                if (Marshal.IsComObject(domains))
                {
                    Marshal.FinalReleaseComObject(domains);
                }

                if (Marshal.IsComObject(messageIndexing))
                {
                    Marshal.FinalReleaseComObject(messageIndexing);
                }

                if (Marshal.IsComObject(settings))
                {
                    Marshal.FinalReleaseComObject(settings);
                }

                if (Marshal.IsComObject(account))
                {
                    Marshal.FinalReleaseComObject(account);
                }

                if (Marshal.IsComObject(application))
                {
                    Marshal.FinalReleaseComObject(application);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [TestMethod]
    public void RegisteredFactory_DeniesDirectSettingsActivationAcrossComBoundary()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(classId, static () => new Settings()));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceSettings).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var settings = (IInterfaceSettings)Marshal.GetObjectForIUnknown(interfacePointer);
                var error = Assert.ThrowsExactly<COMException>(() => _ = settings.MaxSMTPConnections);

                Assert.AreEqual(unchecked((int)0x80070005), error.ErrorCode);
                if (Marshal.IsComObject(settings))
                {
                    Marshal.FinalReleaseComObject(settings);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint context,
        in Guid interfaceId,
        out nint interfacePointer);

    private sealed class TestAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class TestMessageIndexingRuntime(int totalMessageCount) : IMessageIndexingRuntime
    {
        public int TotalMessageCount => totalMessageCount;
        public int TotalIndexedCount => 0;
        public bool Enabled { get; set; }
        public string Backend => string.Empty;
        public bool IsFullTextReady => false;
        public string BackfillStatus => string.Empty;
        public string LastError => string.Empty;
        public void Clear() { }
        public void Index() { }
        public void Rebuild() { }
    }

    private sealed class TestDomainAdministrationStore(IReadOnlyList<DomainAdministrationSnapshot> domains)
        : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(domains);
    }

    private sealed class TestAccountAdministrationStore(IReadOnlyList<AccountAdministrationSnapshot> accounts)
        : IAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                accounts.Where(account => account.DomainId == domainId).ToArray());

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(accounts.FirstOrDefault(account => account.Id == accountId));
    }

    private sealed class TestAliasAdministrationStore(IReadOnlyList<AliasAdministrationSnapshot> aliases)
        : IAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AliasAdministrationSnapshot>>(
                aliases.Where(alias => alias.DomainId == domainId).ToArray());
    }

    private sealed class TestDomainAliasAdministrationStore(IReadOnlyList<DomainAliasAdministrationSnapshot> aliases)
        : IDomainAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAliasAdministrationSnapshot>>(
                aliases.Where(alias => alias.DomainId == domainId).ToArray());
    }

    private sealed class TestDistributionListAdministrationStore(
        IReadOnlyList<DistributionListAdministrationSnapshot> distributionLists)
        : IDistributionListAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(
                distributionLists.Where(list => list.DomainId == domainId).ToArray());
    }

    private sealed class TestDistributionListRecipientAdministrationStore(
        IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients)
        : IDistributionListRecipientAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>(
                recipients.Where(recipient => recipient.ListId == distributionListId).ToArray());
    }
}
