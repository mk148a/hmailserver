namespace HMailServer.ComInterop;

internal sealed class ReinitializationAdmission
{
    private int _running;

    internal bool TryAdmit(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            work();
            return true;
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }
}
