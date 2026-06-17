namespace HMailServer.Core.Abstractions;

public sealed record ClientPasswordValidationScriptResult(
    ClientPasswordValidationScriptDecision Decision)
{
    public static ClientPasswordValidationScriptResult Continue() =>
        new(ClientPasswordValidationScriptDecision.Continue);

    public static ClientPasswordValidationScriptResult Accept() =>
        new(ClientPasswordValidationScriptDecision.Accept);

    public static ClientPasswordValidationScriptResult Reject() =>
        new(ClientPasswordValidationScriptDecision.Reject);
}
