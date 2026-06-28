namespace HMailServer.Core.Abstractions;

public sealed record DirectoryAdministrationSnapshot(
    string ProgramDirectory,
    string DatabaseDirectory,
    string DataDirectory,
    string LogDirectory,
    string TempDirectory,
    string EventDirectory,
    string DBScriptDirectory);
