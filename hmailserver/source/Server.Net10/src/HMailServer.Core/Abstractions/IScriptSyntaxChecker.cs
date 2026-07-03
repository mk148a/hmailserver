namespace HMailServer.Core.Abstractions;

public interface IScriptSyntaxChecker
{
    string CheckSyntax(string language, string scriptFile);
}
