namespace HMailServer.Core.Abstractions;

public sealed record ClientPasswordValidationScriptRequest(
    ScriptAccount Account,
    string Password);
