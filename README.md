hMailServer
===========

hMailServer is an open source email server for Microsoft Windows.

This page describes how to compile and run hMailServer in debug. 

For other information about hMailServer, please go to http://www.hmailserver.com

No active development
=====================

.NET 10 rewrite continuation audit (2026-08-09, DNSBL missing-host HRESULT parity)
------------------------------------------------------------------------------------

Code/test commit `e279ac725` closes the narrow `DNSBlackLists.ItemByDNSHost` COM status gap. Legacy `InterfaceDNSBlackLists::get_ItemByDNSHost` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:168-184`) performs a case-insensitive collection lookup and returns `S_FALSE` (`0x00000001`) when no host matches. The .NET `DNSBlackLists.get_ItemByDNSHost` (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/DnsBlackLists.cs:208-222`) now preserves that HRESULT while retaining case-insensitive hits.

Focused DNSBL coverage is `15 passed, 0 failed, 0 skipped`; DNSBL plus the related SQL integration class is `27 passed, 0 failed, 0 skipped`. Full Net10 is `1961 passed, 31 skipped, 2 failed`; the two failures are the known host-AV locks on generated scanner `.eml` cleanup. IInterfaceDNSBlackLists DISPID 7, direct activation denial, authenticated Settings access, owner-scoped SQL lookup, and SMTP DNSBL behavior are unchanged. Release remains RED: approved disposable SQL/Data restore, live SQL/FTS and protocol/load, service/COM, SEC-18, migration/rollback, AD/DC, and soak gates remain open.

.NET 10 rewrite continuation audit (2026-08-09, obsolete AntiSpam setter parity)
----------------------------------------------------------------------------------

Code/test commit `508d35d17` closes the narrow legacy `AntiSpam.TarpitDelay` and `AntiSpam.TarpitCount` setter gap. Legacy `InterfaceAntiSpam::put_TarpitDelay` and `put_TarpitCount` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:745-792`) authenticate through the attached object, ignore the obsolete values, and return `S_OK`; the getters return `0`. The .NET setters now perform the authenticated facade check and preserve the no-op, while direct activation remains `E_ACCESSDENIED`. `AntiSpamComContractTests` covers authorized no-op behavior and direct-activation denial.

Focused AntiSpam coverage is `15 passed, 0 failed, 0 skipped`; full Net10 is `1961 passed, 31 skipped, 2 failed`. The two failures are the known host-AV locks during generated `.eml` cleanup in `ClamWinScannerTestRuntimeTests` and `CustomScannerTestRuntimeTests`. The parity audit also confirmed that the legacy IMAP domain-alias/default-domain lookup path is already present in `SqlServerImapAccountAuthenticator.AccountLookupSql` and `AuthenticateNormalAsync`; that backlog item is stale and was not restarted. Release remains RED: approved disposable SQL/Data restore, live performance/load, service/COM, SEC-18, migration/rollback, AD/DC, and soak gates remain open.

.NET 10 rewrite continuation audit (2026-08-09, Language.Download HRESULT parity)
----------------------------------------------------------------------------------

Code/test commit `23fd5ef74` aligns authorized `Language.Download()` with legacy `InterfaceLanguage::Download` (`hmailserver/source/Server/COM/InterfaceLanguage.cpp:67`), which calls `COMError::GenerateError("Not implemented.")` (`COMError.cpp:24`) and returns `0x800403E9`. The .NET path (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/Languages.cs:141`) now preserves that HRESULT and message; `GlobalObjectsComContractTests` covers it. IInterfaceLanguage IID/vtable/DISPID 4 and direct activation/access boundaries are unchanged.

Focused GlobalObjects coverage is `8 passed, 0 failed, 0 skipped`; full Net10 is `1961 passed, 31 skipped, 2 failed`, with the same two host-AV scanner cleanup failures. No SQL/Data, IIS, service, registry, DCOM, protocol, or production state changed. Release remains RED and the next gates remain approved disposable SQL/Data restore, live performance/load, and AV-compatible scanner cleanup.

.NET 10 rewrite continuation audit (2026-08-09, release-gate revalidation)
----------------------------------------------------------------------------

The retained Domain child-collection audit found no new production gap. Legacy `InterfaceDomain::get_Accounts`, `get_Aliases`, `get_DomainAliases`, and `get_DistributionLists` (`hmailserver/source/Server/COM/InterfaceDomain.cpp:308-478`) attach the shared authentication state; the .NET `Domain` adapter (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/Domains.cs:811-821,882-889`) evaluates its guarded snapshot before creating each child adapter and propagates the live callback. `DomainsComContractTests`, `LinksComContractTests`, and the route WebAdmin source test pass `27/27`; no production code changed.

The historical `background_route_save.php` POST-only/CSRF item is already complete in `8d684e638` and covered by `WebAdminRoutePostOnlySourceTests`; it was not restarted. The approved disposable SQL/Data restore target remains unset, so populated-store restore, rollback, live SQL/FTS, protocol/load, service/COM, SEC-18, installer, AD/DC, and 24-hour soak gates remain RED. The default full suite remains non-clean because host AV locks generated scanner `.eml` files. Untracked benchmark artifacts contain an older `d7d5cb6c4` run and are not release evidence; the newer temporary benchmark evidence at `565175aff` was not staged.

.NET 10 rewrite continuation audit (2026-08-09, backup creation revalidation)
-------------------------------------------------------------------------------

The formerly recorded raw non-DB-only `BODomains|BOMessages` `DataBackup` staging item is already implemented. Legacy anchors are `BackupExecuter::StartBackup` and `BackupExecuter::BackupDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:57-147,172-217`), `FileUtilities::CopyDirectory`/`DeleteFilesInDirectory`, and `Compression::AddDirectory`; the .NET path is `SevenZipBackupArchiveRuntime.CreateAsync`. Raw mode leaves the external `DataBackup` beside the archive, compressed mode archives staged content, and DB-only mode omits physical staging.

Focused backup creation/restore containment revalidation is `150 passed, 0 failed, 0 skipped`; `check-net10-prereqs.ps1 -RequireMsBuild` passed. The complete option matrix is covered by `BackupArchiveRuntimeTests.CreatesCompleteBackupOptionMatrixWithLegacyOrderingAndCleanup` plus the raw, compressed, and DB-only archive tests. Do not restart the stale raw staging item. The next release gate remains disposable SQL/Data restore acceptance, which requires the approved isolated connection and opt-in.

.NET 10 rewrite continuation audit (2026-08-09, ClamAV local-target rebind hardening)
--------------------------------------------------------------------------------------

Code/test commit `414b1e9e0` closes the bounded ClamAV hostname re-resolution window in the COM test path. Legacy `InterfaceAntiVirus::TestClamAVScanner` (`hmailserver/source/Server/COM/InterfaceAntiVirus.cpp:577-596`) passes the supplied hostname to `VirusScannerTester::TestClamAVConnect` (`hmailserver/source/Server/Common/AntiVirus/VirusScannerTester.cpp:22-45`), which passes it to `ClamAVVirusScanner::Scan` and `SynchronousConnection::Connect` (`hmailserver/source/Server/Common/AntiVirus/ClamAVVirusScanner.cpp:48-64`). The .NET `LegacyLocalScannerTargetGuard.TryGetValidatedLocalAddress` now resolves once, rejects any non-local answer, and `AntiVirus.TestClamAVScanner` passes only the validated IP literal to the existing runtime interface.

Focused guard/ClamAV/AntiVirus coverage is `20 passed, 0 failed, 0 skipped`. Filtered full Net10 is `1954 passed, 0 failed, 31 skipped`; default full is `1959 passed, 2 failed, 31 skipped`. The two default failures remain host-AV cleanup locks on generated `.eml` files in the ClamWin and custom scanner runtime tests. Installed COM identity, direct activation, authentication, SMTP trust, live reconfiguration, SQL/Data, service, IIS, registry, and DCOM state are unchanged. Release remains RED because SQL/Data restore, SEC-18, service/COM, installer, live protocol/load, native restore containment, AD/DC, and soak gates remain open.

.NET 10 rewrite continuation audit (2026-08-09, retained AntiVirus authorization)
----------------------------------------------------------------------------------

Code/test commit `3c8b58981` closes the retained AntiVirus authorization gap. Legacy `InterfaceSettings::get_AntiVirus` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:387-405`) grants the object only to a server administrator, and every public `InterfaceAntiVirus` getter, setter, attachment-blocking member, and scanner-test method rechecks `GetIsServerAdmin` (`hmailserver/source/Server/COM/InterfaceAntiVirus.cpp:20-581`). The .NET `AntiVirus.Snapshot` guard now rechecks the live administrator callback for retained scalar and scanner operations. `BlockedAttachments.GetBlockedAttachments` also fails closed for retained collection operations, including `DeleteByDBID`; this is deliberate security hardening because the legacy collection method itself only checked its attached parent pointer while the .NET child mutation paths already carried live authorization.

Focused AntiVirus/BlockedAttachments coverage is `27 passed, 0 failed, 0 skipped`. Filtered full Net10 is `1951 passed, 0 failed, 31 skipped`; default full is `1956 passed, 2 failed, 31 skipped`, with the two known `UnauthorizedAccessException` cleanup failures caused by the host AV locking generated `.eml` files in `ClamWinScannerTestRuntimeTests` and `CustomScannerTestRuntimeTests`. Installed COM identity, direct activation boundaries, SMTP trust, live reconfiguration, SQL/Data, service, IIS, registry, and DCOM state are unchanged.

The next security slice is the ClamAV hostname DNS-rebind gap: `AntiVirus.TestClamAVScanner` validates a local target, but the runtime client can resolve the hostname again at connection time. It remains unimplemented here. Release remains RED because disposable SQL/Data restore, SEC-18, service/COM, installer, live protocol/load, native restore containment, AD/DC, and soak gates remain open.

.NET 10 rewrite continuation audit (2026-08-09, retained MessageIndexing authorization)
--------------------------------------------------------------------------------------

The .NET 10 branch is a side-by-side rewrite and is not a production release. Code/test commit `e2109f422` carries the live server-administrator callback from `Settings.MessageIndexing` into retained MessageIndexing facades. Legacy `InterfaceSettings::get_MessageIndexing` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1974-1990`) requires server-admin access; `InterfaceMessageIndexing::get_TotalMessageCount`, `get_TotalIndexedCount`, `Clear`, and `Index` (`hmailserver/source/Server/COM/InterfaceMessageIndexing.cpp:64-137`) recheck it, while legacy `get_Enabled`/`put_Enabled` (`:30-62`) do not. The .NET `MessageIndexing2` status properties and `Rebuild` are also guarded because they are retained admin operations; installed COM identity and direct activation boundaries are unchanged. Focused MessageIndexing/Settings coverage is `25 passed, 0 failed, 0 skipped`; filtered full Net10 is `1949 passed, 0 failed, 31 skipped`.

The default full run is `1954 passed, 2 failed, 31 skipped`. Both failures are `UnauthorizedAccessException` cleanup failures in `ClamWinScannerTestRuntimeTests` and `CustomScannerTestRuntimeTests`, where host AV locks generated `.eml` files; excluding those two classes passes. The commandable offline 100,000-message SEARCH/SORT benchmark was rerun at HEAD `565175aff`: Release build 0 warnings/0 errors, correctness and threshold passed, p50/p95/p99 `6.839/13.904/16.184 ms`, with JSON/CSV/Markdown written to a temporary directory. It remains diagnostic only, not live SQL FTS, protocol, concurrency, C++ equivalence, or soak evidence.

The post-MessageIndexing parity audit rejected retained `Settings.ServerMessages` as a false gap because legacy `InterfaceServerMessages` authorizes at acquisition and attaches authentication only to child construction; it rejected `GlobalObjects.Languages` callback propagation because legacy `InterfaceGlobalObjects::get_Languages`, `InterfaceLanguages`, and `InterfaceLanguage` permit retained reads after authentication loss. No code slice was committed. The next executable priority is approved disposable SQL/Data restore acceptance; its integration connection and isolated-create opt-in remain unset.

Legacy `InterfaceAccount::ValidatePassword` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:350-364`) validates the attached account through `PasswordValidator::ValidatePassword`, including legacy hash modes, AD validation, and the client password event. The current `Account.ValidatePassword` (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AccountComClass.cs:417-426`) remains deliberately fenced for SQL-backed snapshots because a safe implementation needs an authoritative credential lookup, retained-object reauthentication, and separately reviewed COM/AD/script boundaries. Do not remove the `E_NOTIMPL` fence as a mechanical parity change.

Production SQL/Data, service/COM, SEC-18, installer, AD/DC, native restore containment, live protocol, and 24-hour soak evidence remain blocked or incomplete. Release status is RED.

hMailServer is no longer being actively developed or maintained. The latest major version was released several years ago. hMailServer relies on algorithms which are considered insecure by modern standards, such as SHA1 and outdated versions of OpenSSL. For that reason, it's recommended that you migrate to an alternative software or service.

Building hMailServer
====================

Branches
--------

   * The master branch contains the latest development version of hMailServer. This version is typically not yet released for production usage. If you want to add new features to hMailServer, use this branch.
   
   * The x.y.z (for example 5.6.2) contains the code for the version with the same name as the branch. For example, branch 5.6.1 contains hMailServer version 5.6.1. These branches are typically only used for bugfixes or minor features.

Environment set up
---------------------

**Required software**

   * An installed version of hMailServer 5.7 (configured with a database)
   * Visual Studio 2019 Community edition
   * InnoSetup 5.5.4a (non-unicode version)
   * Perl 5 (https://strawberryperl.com/)
   * Python 3 (https://www.python.org/)
   
**NOTE**

You should not be compiling hMailServer on a computer which already runs a production version of hMailServer. When compiling hMailServer, the compilation will stop any already running version of hMailServer, and will register the compiled version as the hMailServer version on the machine (configuring the Windows service). This means that if you are running a production version of hMailServer on the machine, this version will stop running if you compile hMailServer. If this happens, the easiest path is to reinstall the production version.

Installing Visual Studio 2019 Community edition
----------------------------------------------

1. Download [Visual Studio 2019](https://visualstudio.microsoft.com/vs/) and launch the installation.
2. Select the following _Workloads_
  * .NET desktop development
  * Desktop development with C++
3. Select the following _Individual components_
  * C++ ATL for latest v142 build tools (x86 & x64)
  * Windows 10 SDK (10.0.18362.0)

3rd party libraries
-------------------

Some 3rd party libraries which hMailServer relies on are large and updated frequently. Rather than including these large libraries into the hMailServer git repository, they have to be downloaded and built, currently manually. When you build hMailServer, Visual Studio will use a system environment variable, named hMailServerLibs, to locate these libraries.

Create an environment variable named hMailServerLibs pointing at a folder where you will store hMailServer libraries, such as C:\Dev\hMailLibs.

Building OpenSSL
----------------
1. Download OpenSSL 3.5.x from http://www.openssl.org/source/ and put it into %hMailServerLibs%\<OpenSSL-Version>.
   You should now have a folder named %hMailServerLibs%\<OpenSSL-version>, for example C:\Dev\hMailLibs\openssl-3.5.5
2. Start a x64 Native Tools Command Prompt for VS2019.
3. Change dir to %hMailServerLibs%\<OpenSSL-version>.
3. Run the following commands:

   <pre>
   SET CFLAGS=-DOPENSSL_TLS_SECURITY_LEVEL=0
   Perl Configure no-asm VC-WIN64A --prefix=%cd%\out64 --openssldir=%cd%\out64 -D_WIN32_WINNT=0x600 --api=1.1.1 no-deprecated
   nmake clean
   nmake install_sw
   </pre>

Building PostgreSQL
-------------------
1. Download PostgreSQL 18.3 source from https://www.postgresql.org/ftp/source/v18.3/ and put it into %hMailServerLibs%\postgresql-18.3.
   You should now have a folder named %hMailServerLibs%\postgresql-18.3, for example C:\Dev\hMailLibs\postgresql-18.3
2. Download winflexbison from https://github.com/lexxmark/winflexbison/releases, extract it, and add the folder to `%PATH%`.
3. Install Python dependencies: `py -m pip install meson ninja`
4. Start a x64 Native Tools Command Prompt for VS2019.
5. Change dir to %hMailServerLibs%
6. Run the following commands:

   <pre>
   set hMailServerLibs=%cd%
   cd postgresql-18.3
   meson setup builddir -Dssl=openssl -Dextra_include_dirs=%hMailServerLibs%\openssl-3.5.5\out64\include -Dextra_lib_dirs=%hMailServerLibs%\openssl-3.5.5\out64\lib
   meson compile -C builddir src/interfaces/libpq/libpq:shared_library
   </pre>

**NOTE:** The `-Dextra_include_dirs` and `-Dextra_lib_dirs` flags ensure meson links against the specific OpenSSL version built above. Verify that no other OpenSSL installation appears earlier in `%PATH%` (e.g. from Git for Windows or other tools), as meson may pick up the wrong version.

**TIP:** You can use [Dependencies](https://github.com/lucasg/Dependencies/releases) to verify that the built `libpq.dll` links against the correct OpenSSL DLLs (`libcrypto-3-x64.dll` / `libssl-3-x64.dll`) and not some other version found elsewhere on the system.

Building Boost
--------------
1. Download Boost 1.90.0 from http://www.boost.org/ and put it into %hMailServerLibs%\<Boost-Version>.
   You should now have a folder named %hMailServerLibs%\<Boost-Version>, for example C:\Dev\hMailLibs\boost_1_90_0
2. Start a x64 Native Tools Command Prompt for VS2019.
3. Change dir to %hMailServerLibs%\<Boost-Version>.
4. Run the following commands:

   NOTE: Change the -j parameter from 4 to the number of cores on your computer. The parameter specifies the number of parallel compilations will be done.

   <pre>
   bootstrap
   b2 debug release threading=multi link=static --with-thread --with-filesystem --with-regex --with-chrono --with-system --with-atomic --toolset=msvc-14.2 address-model=64 stage --build-dir=out64 -j 4
   </pre>

Building hMailServer
--------------------

Visual Studio 2019 must be started with _Run as Administrator_.

1. Download the source code from this Git repository.
2. Compile the solution hmailserver\source\Server\hMailServer\hMailServer.sln.
   This will build the hMailServer server-part (hMailServer.exe)
3. Compile the solution hmailserver\source\Tools\hMailServer Tools.sln.
   This will build hMailServer related tools, such as hMailServer Administrator and hMailServer DB Setup.
4. Compile hmailserver\installation\hMailServer.iss (using InnoSetup)
   This will build the hMailServer installation program.

Running in Debug
----------------

If you want to run hMailServer in debug mode in Visual Studio, add the command argument /debug. You find this setting in the Project properties, under Configuration Properties -> Debugging.

Running tests
-------------

hMailServer source code contains a number of automated tests which excercises the basic functionality. When adding new features or fixing bugs, corresponding tests should be added. hMailServer tests are implemented using NUnit. To run them in Visual Studio, follow these steps:

NOTE: When running tests, your local hMailServer installation will be updated with test accounts. Existing domains and accounts are deleted. Each tests prepares the server configuration in different ways. In other words, do not run the automated tests in an environment where you need to preserve hMailServer data.

1. Make sure hMailServer.exe is built and can be run. The tests will launch the service.
2. Open the test solution, `\hmailserver\test\hMailServer Tests.sln`
3. In Visual Studio, select Test Explorer from the View-menu. 
4. Locate a test to run under "RegressionTests"
5. Right-click on a test or test category and select "Run".

You can also navigate to the source code for a test, right-click anywhere and select "Run Test(s)" to run it.

Releasing hMailServer
=====================

Without finding any serious issues:

1. Run all integration tests on supported versions of Windows and the different supported databases. 
2. Run all server stress tests
3. Enable Gflags (gflags /p /enable hmailserver.exe) and run all integration tests to check for memory issues
4. Run for at least 1 week in production for hMailServer.com
5. Wait for at least 500 downloads of the beta version
