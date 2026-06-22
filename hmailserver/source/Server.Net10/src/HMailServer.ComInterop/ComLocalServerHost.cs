using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed class ComLocalServerRegistration
{
    public ComLocalServerRegistration(Guid classId, Func<object> createInstance)
    {
        ArgumentNullException.ThrowIfNull(createInstance);

        ClassId = classId;
        CreateInstance = createInstance;
    }

    public Guid ClassId { get; }

    internal Func<object> CreateInstance { get; }
}

[ComVisible(false)]
[SupportedOSPlatform("windows")]
public sealed class ComLocalServerHost : IDisposable
{
    private const uint CoinitMultithreaded = 0;
    private const uint ClsctxLocalServer = 0x4;
    private const uint RegclsMultipleUse = 0x1;
    private const uint RegclsSuspended = 0x4;

    private readonly IReadOnlyList<ComLocalServerRegistration> _registrations;
    private readonly ManualResetEventSlim _stop = new(initialState: false);
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _syncRoot = new();

    private Thread? _thread;
    private bool _disposed;

    public ComLocalServerHost(params ComLocalServerRegistration[] registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        if (registrations.Length == 0)
        {
            throw new ArgumentException("At least one COM class registration is required.", nameof(registrations));
        }

        _registrations = registrations.ToArray();
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_thread is not null)
            {
                throw new InvalidOperationException("The COM local-server host has already been started.");
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "hMailServer COM local-server host"
            };
            _thread.Start();
        }

        _started.Task.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Thread? thread;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            thread = _thread;
            _stop.Set();
        }

        if (thread is not null && thread.ManagedThreadId != Environment.CurrentManagedThreadId)
        {
            thread.Join();
        }

        _stop.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Run()
    {
        var initialized = false;
        var classObjects = new List<RegisteredClassObject>(_registrations.Count);

        try
        {
            Marshal.ThrowExceptionForHR(CoInitializeEx(nint.Zero, CoinitMultithreaded));
            initialized = true;

            foreach (var registration in _registrations)
            {
                var factory = new ComClassFactory(registration.CreateInstance);
                var factoryPointer = Marshal.GetIUnknownForObject(factory);

                try
                {
                    var classId = registration.ClassId;
                    Marshal.ThrowExceptionForHR(CoRegisterClassObject(
                        in classId,
                        factoryPointer,
                        ClsctxLocalServer,
                        RegclsMultipleUse | RegclsSuspended,
                        out var cookie));
                    classObjects.Add(new RegisteredClassObject(cookie, factory));
                }
                finally
                {
                    Marshal.Release(factoryPointer);
                }
            }

            Marshal.ThrowExceptionForHR(CoResumeClassObjects());
            _started.SetResult();
            _stop.Wait();
        }
        catch (Exception exception)
        {
            _started.TrySetException(exception);
        }
        finally
        {
            for (var index = classObjects.Count - 1; index >= 0; index--)
            {
                _ = CoRevokeClassObject(classObjects[index].Cookie);
            }

            if (initialized)
            {
                CoUninitialize();
            }
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoInitializeEx(nint reserved, uint coInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoRegisterClassObject(
        in Guid classId,
        nint classObject,
        uint context,
        uint flags,
        out uint cookie);

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoResumeClassObjects();

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoRevokeClassObject(uint cookie);

    private sealed record RegisteredClassObject(uint Cookie, ComClassFactory Factory);
}

[ComVisible(true)]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IComClassFactory
{
    [PreserveSig]
    int CreateInstance(nint outer, in Guid interfaceId, out nint instance);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool lockServer);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[SupportedOSPlatform("windows")]
internal sealed class ComClassFactory(Func<object> createInstance) : IComClassFactory
{
    private const int ClassENoAggregation = unchecked((int)0x80040110);

    public int CreateInstance(nint outer, in Guid interfaceId, out nint instance)
    {
        instance = nint.Zero;

        if (outer != nint.Zero)
        {
            return ClassENoAggregation;
        }

        nint unknown = nint.Zero;

        try
        {
            var managedInstance = createInstance()
                ?? throw new InvalidOperationException("The COM class factory returned a null instance.");
            unknown = Marshal.GetIUnknownForObject(managedInstance);
            return Marshal.QueryInterface(unknown, in interfaceId, out instance);
        }
        catch (Exception exception)
        {
            return Marshal.GetHRForException(exception);
        }
        finally
        {
            if (unknown != nint.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    public int LockServer(bool lockServer) => 0;
}
