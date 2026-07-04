namespace HMailServer.Core.Abstractions;

public enum DkimVerificationResult
{
    Neutral = 0,
    Pass = 1,
    TempFail = 2,
    PermFail = 3
}

public interface IDkimVerificationRuntime
{
    DkimVerificationResult Verify(string file);
}
