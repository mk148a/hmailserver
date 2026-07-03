using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace HMailServer.Scripting;

public sealed class WindowsScriptRuntimeReloader : IScriptRuntimeReloader
{
    internal const int SyntaxErrorEventId = 5016;
    internal const int LoadErrorEventId = 5017;

    private readonly IScriptSyntaxChecker _syntaxChecker;
    private readonly ILogger<WindowsScriptRuntimeReloader> _logger;

    public WindowsScriptRuntimeReloader(
        IScriptSyntaxChecker syntaxChecker,
        ILogger<WindowsScriptRuntimeReloader> logger)
    {
        ArgumentNullException.ThrowIfNull(syntaxChecker);
        ArgumentNullException.ThrowIfNull(logger);
        _syntaxChecker = syntaxChecker;
        _logger = logger;
    }

    public void Reload(string language, string scriptFile)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptFile);

        try
        {
            var syntaxError = _syntaxChecker.CheckSyntax(language, scriptFile);
            if (syntaxError.Length > 0)
            {
                _logger.LogError(
                    new EventId(SyntaxErrorEventId, "ScriptSyntaxError"),
                    "Script reload rejected the configured event script: {SyntaxError}",
                    syntaxError);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                new EventId(LoadErrorEventId, "ScriptLoadError"),
                exception,
                "An exception was thrown while reloading the configured event script.");
        }
    }
}
