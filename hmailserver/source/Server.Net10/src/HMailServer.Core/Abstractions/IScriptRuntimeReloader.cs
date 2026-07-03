namespace HMailServer.Core.Abstractions;

public interface IScriptRuntimeReloader
{
    void Reload(string language, string scriptFile);
}
