using System.Diagnostics;
using System.Xml.Linq;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BackupRawSqlDataStagingIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RawDomainsAndMessagesBackup_StagesDisposableDataBackupSiblingAndPreservesNestedMessageFiles()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var targetName = $"hmail_perf_backup_raw_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var databaseConnectionString = WithDatabase(serverConnectionString, targetName);
        var root = Path.Combine(Path.GetTempPath(), targetName);
        var dataDirectory = Path.Combine(root, "Data");
        var destination = Path.Combine(root, "Destination");
        var nestedMessagePath = Path.Combine(
            dataDirectory,
            "legacy.example",
            "user",
            "nested",
            "message.eml");
        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");

        if (!File.Exists(sevenZipPath))
        {
            Assert.Inconclusive($"The test fixture is missing {sevenZipPath}.");
        }

        await CreateDatabaseAsync(masterConnectionString, targetName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(databaseConnectionString).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(nestedMessagePath)!);
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(nestedMessagePath, "From: sender@example.test\r\n\r\nraw message")
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "root-file.txt"), "must be omitted")
                .ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(databaseConnectionString);
            var payloadRuntime = new BackupXmlPayloadRuntime(
                new SqlServerSettingsAdministrationStore(factory),
                new SqlServerDomainAdministrationStore(factory),
                new SqlServerDomainAliasAdministrationStore(factory),
                new SqlServerAccountAdministrationStore(factory),
                new SqlServerAliasAdministrationStore(factory),
                distributionListStore: null,
                distributionListRecipientStore: null,
                folderStore: new SqlServerImapFolderAdministrationStore(factory),
                messageStore: new SqlServerMessageAdministrationStore(factory));
            var archiveRuntime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-test",
                localNow: static () => new DateTime(2026, 9, 1, 1, 2, 3),
                payloadProvider: payloadRuntime.GetPayloadAsync,
                dataDirectory: dataDirectory);
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: BackupStartPlan.BackupDomainsFlag | BackupStartPlan.BackupMessagesFlag,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);

            await archiveRuntime.CreateAsync(evidence, CancellationToken.None).ConfigureAwait(false);

            var archivePath = Path.Combine(destination, "HMBackup 2026-09-01 010203.7z");
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            var metadataXml = new SevenZipBackupArchiveMetadataReader(sevenZipPath)
                .ReadMetadataXml(archivePath);
            var document = XDocument.Parse(metadataXml);
            var backupInformation = document.Root!.Element("BackupInformation")!;
            var dataFiles = backupInformation.Element("DataFiles")!;
            Assert.AreEqual("6", (string?)backupInformation.Attribute("Mode"));
            Assert.AreEqual("Raw", (string?)dataFiles.Attribute("Format"));
            Assert.AreEqual("DataBackup", (string?)dataFiles.Attribute("FolderName"));
            Assert.AreEqual(
                "legacy.example",
                (string?)document.Root.Element("Domains")!.Element("Domain")!.Attribute("Name"));
            Assert.AreEqual(
                "message.eml",
                (string?)document.Descendants("Message").Single().Attribute("Filename"));

            var dataBackup = Path.Combine(destination, "DataBackup");
            Assert.IsTrue(Directory.Exists(dataBackup));
            Assert.AreEqual(destination, Directory.GetParent(dataBackup)!.FullName);
            Assert.IsTrue(File.Exists(Path.Combine(
                dataBackup,
                "legacy.example",
                "user",
                "nested",
                "message.eml")));
            Assert.IsFalse(File.Exists(Path.Combine(dataBackup, "root-file.txt")));
            Assert.IsFalse(Directory.Exists(Path.Combine(dataDirectory, "DataBackup")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "hMailServerBackup.xml")));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, targetName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task CompressedDomainsAndMessagesBackup_EmbedsDataBackupAndCleansStagingSibling()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var targetName = $"hmail_perf_backup_compressed_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var databaseConnectionString = WithDatabase(serverConnectionString, targetName);
        var root = Path.Combine(Path.GetTempPath(), targetName);
        var dataDirectory = Path.Combine(root, "Data");
        var destination = Path.Combine(root, "Destination");
        var nestedMessagePath = Path.Combine(
            dataDirectory,
            "legacy.example",
            "user",
            "nested",
            "message.eml");
        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");

        if (!File.Exists(sevenZipPath))
        {
            Assert.Inconclusive($"The test fixture is missing {sevenZipPath}.");
        }

        await CreateDatabaseAsync(masterConnectionString, targetName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(databaseConnectionString).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(nestedMessagePath)!);
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(nestedMessagePath, "From: sender@example.test\r\n\r\ncompressed message")
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "root-file.txt"), "must be omitted")
                .ConfigureAwait(false);

            var factory = new SqlServerConnectionFactory(databaseConnectionString);
            var payloadRuntime = new BackupXmlPayloadRuntime(
                new SqlServerSettingsAdministrationStore(factory),
                new SqlServerDomainAdministrationStore(factory),
                new SqlServerDomainAliasAdministrationStore(factory),
                new SqlServerAccountAdministrationStore(factory),
                new SqlServerAliasAdministrationStore(factory),
                distributionListStore: null,
                distributionListRecipientStore: null,
                folderStore: new SqlServerImapFolderAdministrationStore(factory),
                messageStore: new SqlServerMessageAdministrationStore(factory));
            var archiveRuntime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-test",
                localNow: static () => new DateTime(2026, 9, 1, 1, 2, 4),
                payloadProvider: payloadRuntime.GetPayloadAsync,
                dataDirectory: dataDirectory);
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: BackupStartPlan.BackupDomainsFlag
                    | BackupStartPlan.BackupMessagesFlag
                    | BackupStartPlan.BackupCompressionFlag,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);

            await archiveRuntime.CreateAsync(evidence, CancellationToken.None).ConfigureAwait(false);

            var archivePath = Path.Combine(destination, "HMBackup 2026-09-01 010204.7z");
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            var metadataXml = new SevenZipBackupArchiveMetadataReader(sevenZipPath)
                .ReadMetadataXml(archivePath);
            var document = XDocument.Parse(metadataXml);
            var backupInformation = document.Root!.Element("BackupInformation")!;
            var dataFiles = backupInformation.Element("DataFiles")!;
            Assert.AreEqual("14", (string?)backupInformation.Attribute("Mode"));
            Assert.AreEqual("7z", (string?)dataFiles.Attribute("Format"));
            Assert.AreEqual("0", (string?)dataFiles.Attribute("Size"));
            Assert.IsTrue(SevenZipContainsEntry(sevenZipPath, archivePath, "DataBackup/legacy.example/user/nested/message.eml"));
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "DataBackup")));
            Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, "root-file.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "hMailServerBackup.xml")));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, targetName).ConfigureAwait(false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        Assert.IsFalse(Directory.Exists(root));
    }

    private static bool SevenZipContainsEntry(string sevenZipPath, string archivePath, string expectedEntry)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = sevenZipPath,
            Arguments = $"l -slt \"{archivePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, process.StandardError.ReadToEnd());
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.StartsWith("Path = ", StringComparison.Ordinal)
                ? line[7..].Replace('\\', '/')
                : line)
            .Any(line => line.Equals(expectedEntry, StringComparison.Ordinal));
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var allowDatabaseCreate = Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(rawConnectionString)
            || !string.Equals(allowDatabaseCreate, "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(rawConnectionString);
        }
        catch (ArgumentException exception)
        {
            Assert.Inconclusive($"The SQL integration connection string is invalid: {exception.Message}");
            throw;
        }

        if (!IsApprovedLocalDataSource(builder.DataSource)
            || !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive(
                "The SQL integration fixture only accepts a local SQL/LocalDB target without AttachDbFilename.");
        }

        return builder.ConnectionString;
    }

    private static bool IsApprovedLocalDataSource(string dataSource)
    {
        var normalized = dataSource.Trim();
        return normalized.Equals(".", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE dbo.hm_settings (
                settingname nvarchar(30) NOT NULL PRIMARY KEY,
                settingstring nvarchar(4000) NOT NULL,
                settinginteger bigint NOT NULL
            );
            CREATE TABLE dbo.hm_domains (
                domainid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                domainname nvarchar(255) NOT NULL,
                domainactive tinyint NOT NULL,
                domainpostmaster nvarchar(255) NOT NULL,
                domainmaxmessagesize int NOT NULL,
                domainuseplusaddressing tinyint NOT NULL,
                domainplusaddressingchar nvarchar(1) NOT NULL,
                domainaddomain nvarchar(255) NOT NULL,
                domainmaxsize int NOT NULL,
                domainmaxnoofaccounts int NOT NULL,
                domainmaxnoofaliases int NOT NULL,
                domainmaxnoofdistributionlists int NOT NULL,
                domainlimitationsenabled tinyint NOT NULL,
                domainmaxaccountsize int NOT NULL,
                domainenablesignature tinyint NOT NULL,
                domainsignaturemethod tinyint NOT NULL,
                domainsignatureplaintext nvarchar(max) NOT NULL,
                domainsignaturehtml nvarchar(max) NOT NULL,
                domainaddsignaturestoreplies tinyint NOT NULL,
                domainaddsignaturestolocalemail tinyint NOT NULL,
                domainantispamoptions int NOT NULL,
                domaindkimselector nvarchar(255) NOT NULL,
                domaindkimprivatekeyfile nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_accounts (
                accountid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                accountdomainid int NOT NULL,
                accountaddress nvarchar(255) NOT NULL,
                accountpassword nvarchar(255) NOT NULL,
                accountactive tinyint NOT NULL,
                accountisad tinyint NOT NULL,
                accountaddomain nvarchar(255) NOT NULL,
                accountadusername nvarchar(255) NOT NULL,
                accountmaxsize int NOT NULL,
                accountvacationmessageon tinyint NOT NULL,
                accountvacationmessage nvarchar(1000) NOT NULL,
                accountvacationsubject nvarchar(200) NOT NULL,
                accountvacationexpires tinyint NOT NULL,
                accountvacationexpiredate datetime NOT NULL,
                accountvacationabortspamflagged tinyint NOT NULL,
                accountpwencryption tinyint NOT NULL,
                accountadminlevel tinyint NOT NULL,
                accountforwardenabled tinyint NOT NULL,
                accountforwardaddress nvarchar(255) NOT NULL,
                accountforwardkeeporiginal tinyint NOT NULL,
                accountforwardabortspamflagged tinyint NOT NULL,
                accountenablesignature tinyint NOT NULL,
                accountsignatureplaintext nvarchar(max) NOT NULL,
                accountsignaturehtml nvarchar(max) NOT NULL,
                accountlastlogontime datetime NOT NULL,
                accountpersonfirstname nvarchar(60) NOT NULL,
                accountpersonlastname nvarchar(60) NOT NULL
            );
            CREATE TABLE dbo.hm_messages (
                messageid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                messageaccountid int NOT NULL,
                messagefolderid int NOT NULL,
                messagefilename nvarchar(255) NOT NULL,
                messagetype tinyint NOT NULL,
                messagefrom nvarchar(255) NOT NULL,
                messagesize bigint NOT NULL,
                messagecurnooftries int NOT NULL,
                messagenexttrytime datetime NOT NULL,
                messageflags tinyint NOT NULL,
                messagecreatetime datetime NOT NULL,
                messagelocked tinyint NOT NULL,
                messageuid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_aliases (
                aliasid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                aliasdomainid int NOT NULL,
                aliasname nvarchar(255) NOT NULL,
                aliasvalue nvarchar(255) NOT NULL,
                aliasactive tinyint NOT NULL
            );
            CREATE TABLE dbo.hm_domain_aliases (
                daid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                dadomainid int NOT NULL,
                daalias nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_imapfolders (
                folderid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                folderaccountid int NOT NULL,
                folderparentid int NOT NULL,
                foldername nvarchar(255) NOT NULL,
                folderissubscribed tinyint NOT NULL,
                foldercurrentuid int NOT NULL,
                foldercreationtime datetime NOT NULL
            );
            INSERT INTO dbo.hm_domains
                (domainname, domainactive, domainpostmaster, domainmaxmessagesize,
                 domainuseplusaddressing, domainplusaddressingchar, domainaddomain,
                 domainmaxsize, domainmaxnoofaccounts, domainmaxnoofaliases,
                 domainmaxnoofdistributionlists, domainlimitationsenabled, domainmaxaccountsize,
                 domainenablesignature, domainsignaturemethod, domainsignatureplaintext,
                 domainsignaturehtml, domainaddsignaturestoreplies, domainaddsignaturestolocalemail,
                 domainantispamoptions, domaindkimselector, domaindkimprivatekeyfile)
            VALUES
                (N'legacy.example', 1, N'postmaster@legacy.example', 1024,
                 1, N'+', N'', 0, 5, 5, 5, 0, 128, 0, 1, N'', N'', 0, 1, 0, N'', N'');
            INSERT INTO dbo.hm_accounts
                (accountdomainid, accountaddress, accountpassword, accountactive, accountisad,
                 accountaddomain, accountadusername, accountmaxsize, accountvacationmessageon,
                 accountvacationmessage, accountvacationsubject, accountvacationexpires,
                 accountvacationexpiredate, accountvacationabortspamflagged, accountpwencryption,
                 accountadminlevel, accountforwardenabled, accountforwardaddress,
                 accountforwardkeeporiginal, accountforwardabortspamflagged, accountenablesignature,
                 accountsignatureplaintext, accountsignaturehtml, accountlastlogontime,
                 accountpersonfirstname, accountpersonlastname)
            VALUES
                (1, N'user@legacy.example', N'', 1, 0, N'', N'', 128, 0, N'', N'', 0,
                 '1901-01-01', 0, 1, 0, 0, N'', 0, 0, 0, N'', N'', '2026-09-01', N'', N'');
            INSERT INTO dbo.hm_imapfolders
                (folderaccountid, folderparentid, foldername, folderissubscribed,
                 foldercurrentuid, foldercreationtime)
            VALUES (1, -1, N'INBOX', 1, 4, '2026-09-01 01:00:00');
            INSERT INTO dbo.hm_messages
                (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
                 messagesize, messagecurnooftries, messagenexttrytime, messageflags,
                 messagecreatetime, messagelocked, messageuid)
            VALUES
                (1, 1, N'message.eml', 2, N'sender@example.test', 42, 0,
                 '1901-01-01', 1, '2026-09-01 01:01:00', 0, 4);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
