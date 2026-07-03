using System.Runtime.InteropServices;
using System.Text;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public interface ILoggingLiveLogRuntime
{
    bool Enabled { get; }

    void Enable(bool enabled);

    void Append(string message);

    string ReadAndClear();
}

[ComVisible(false)]
public sealed class ProcessLocalLoggingLiveLogRuntime : ILoggingLiveLogRuntime
{
    internal const int LegacyMaximumLength = 1_000_000;

    private readonly Lock _syncRoot = new();
    private readonly StringBuilder _buffer = new();
    private bool _enabled;

    public bool Enabled
    {
        get
        {
            lock (_syncRoot)
            {
                return _enabled;
            }
        }
    }

    public void Enable(bool enabled)
    {
        lock (_syncRoot)
        {
            _buffer.Clear();
            _enabled = enabled;
        }
    }

    public void Append(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_syncRoot)
        {
            if (!_enabled)
            {
                return;
            }

            _buffer.Append(message);
            if (_buffer.Length > LegacyMaximumLength)
            {
                _buffer.Clear();
                _enabled = false;
            }
        }
    }

    public string ReadAndClear()
    {
        lock (_syncRoot)
        {
            var result = _buffer.ToString();
            _buffer.Clear();
            return result;
        }
    }
}

[ComVisible(false)]
public static class LoggingLiveLogRuntimeHost
{
    private static readonly ILoggingLiveLogRuntime Runtime = new ProcessLocalLoggingLiveLogRuntime();

    public static void Append(string message) => Runtime.Append(message);

    public static ILoggingLiveLogRuntime Current => Runtime;
}
