using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal static class WindowsHandleRelativeDirectoryCopier
{
    private const uint FileListDirectory = 0x00000001;
    private const uint FileAddFile = 0x00000002;
    private const uint FileAddSubdirectory = 0x00000004;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileReadData = 0x00000001;
    private const uint FileWriteData = 0x00000002;
    private const uint Synchronize = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint Win32OpenExisting = 3;
    private const uint NtOpenExisting = 1;
    private const uint NtCreateNew = 2;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileSynchronousIoNonalert = 0x00000020;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint DirectoryTraversalAccess = FileListDirectory | FileReadAttributes | Synchronize;
    private const uint DestinationDirectoryAccess =
        DirectoryTraversalAccess | FileAddFile | FileAddSubdirectory;
    private const uint ObjCaseInsensitive = 0x00000040;
    private const int FileDirectoryInformation = 1;
    private const int StatusSuccess = 0;
    private const int StatusBufferOverflow = unchecked((int)0x80000005);
    private const int StatusNoMoreFiles = unchecked((int)0x80000006);
    private const int StatusNoSuchFile = unchecked((int)0xC000000F);
    private const uint FileAttributeReparsePoint = 0x00000400;

    internal static void Copy(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The handle-relative directory copier is supported only on Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        using var source = OpenDirectoryPath(Path.GetFullPath(sourcePath), "source");
        using var destination = OpenOrCreateDirectoryPath(
            Path.GetFullPath(destinationPath),
            "destination");
        CopyDirectory(source, destination, cancellationToken);
    }

    internal static void EnsureDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        using var directory = OpenOrCreateDirectoryPath(Path.GetFullPath(path), "destination");
    }

    internal static SafeFileHandle OpenExistingDirectoryPath(
        string path,
        string description,
        uint finalDesiredAccess)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The handle-relative directory opener is supported only on Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        if (rootPath is null)
        {
            throw new IOException($"The {description} directory path has no root.");
        }

        var components = fullPath[rootPath.Length..]
            .Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0)
        {
            return OpenDirectoryPath(rootPath, $"{description} root", finalDesiredAccess);
        }

        var current = OpenDirectoryPath(rootPath, $"{description} root");
        try
        {
            for (var index = 0; index < components.Length; index++)
            {
                var next = OpenExistingDirectory(
                    current,
                    components[index],
                    index == components.Length - 1
                        ? finalDesiredAccess
                        : DirectoryTraversalAccess,
                    description);
                current.Dispose();
                current = next;
            }

            var result = current;
            current = null!;
            return result;
        }
        finally
        {
            current?.Dispose();
        }
    }

    internal static string ComputeSha256(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The handle-relative directory hashing is supported only on Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var source = OpenDirectoryPath(Path.GetFullPath(path), "source");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendDirectoryHash(hash, source, relativePath: string.Empty);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void CopyDirectory(
        SafeFileHandle source,
        SafeFileHandle destination,
        CancellationToken cancellationToken)
    {
        foreach (var entry in EnumerateDirectory(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((entry.Attributes & FileAttributeReparsePoint) != 0)
            {
                throw new IOException("The directory copy source contains a reparse point.");
            }

            if ((entry.Attributes & (uint)FileAttributes.Directory) != 0)
            {
                using var sourceDirectory = OpenRelativeDirectory(source, entry.Name);
                EnsureNotReparsePoint(sourceDirectory, "source directory");
                using var destinationDirectory = CreateRelativeDirectory(destination, entry.Name);
                EnsureNotReparsePoint(destinationDirectory, "destination directory");
                CopyDirectory(sourceDirectory, destinationDirectory, cancellationToken);
                continue;
            }

            using var sourceFile = OpenRelativeFile(source, entry.Name, FileReadData | FileReadAttributes | Synchronize);
            EnsureNotReparsePoint(sourceFile, "source file");
            using var destinationFile = CreateRelativeFile(destination, entry.Name);
            EnsureNotReparsePoint(destinationFile, "destination file");
            CopyFile(sourceFile, destinationFile, cancellationToken);
        }
    }

    private static void AppendDirectoryHash(
        IncrementalHash hash,
        SafeFileHandle directory,
        string relativePath)
    {
        AppendHashValue(hash, "D\0" + relativePath);
        var entries = EnumerateDirectory(directory);

        foreach (var entry in entries
            .Where(static entry => (entry.Attributes & (uint)FileAttributes.Directory) != 0)
            .OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            using var childDirectory = OpenRelativeDirectory(directory, entry.Name);
            EnsureNotReparsePoint(childDirectory, "source directory");
            AppendDirectoryHash(hash, childDirectory, CombineRelative(relativePath, entry.Name));
        }

        foreach (var entry in entries
            .Where(static entry => (entry.Attributes & (uint)FileAttributes.Directory) == 0)
            .OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            using var file = OpenRelativeFile(directory, entry.Name, FileReadData | FileReadAttributes | Synchronize);
            EnsureNotReparsePoint(file, "source file");
            AppendHashValue(hash, "F\0" + CombineRelative(relativePath, entry.Name));
            AppendFileHash(hash, file);
        }
    }

    private static void AppendFileHash(IncrementalHash hash, SafeFileHandle file)
    {
        using var stream = new FileStream(file, FileAccess.Read, 64 * 1024, isAsync: false);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
        }
    }

    private static string CombineRelative(string parent, string child) =>
        string.IsNullOrEmpty(parent) ? child : parent + "/" + child;

    private static void AppendHashValue(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData(new byte[] { 0 });
    }

    private static void CopyFile(
        SafeFileHandle source,
        SafeFileHandle destination,
        CancellationToken cancellationToken)
    {
        using var sourceStream = new FileStream(source, FileAccess.Read, 64 * 1024, isAsync: false);
        using var destinationStream = new FileStream(destination, FileAccess.Write, 64 * 1024, isAsync: false);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = sourceStream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            destinationStream.Write(buffer, 0, read);
        }

        destinationStream.Flush(flushToDisk: true);
    }

    private static SafeFileHandle OpenOrCreateDirectoryPath(string path, string description)
    {
        var rootPath = Path.GetPathRoot(path);
        if (rootPath is null)
        {
            throw new IOException($"The {description} directory path has no root.");
        }

        var components = path[rootPath.Length..]
            .Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
        var handles = new List<SafeFileHandle>
        {
            OpenDirectoryPath(rootPath, $"{description} root")
        };
        var hasDestinationAccess = new List<bool> { false };
        try
        {
            foreach (var component in components)
            {
                var parent = handles[^1];
                var parentHasDestinationAccess = hasDestinationAccess[^1];
                SafeFileHandle next;
                var nextHasDestinationAccess = parentHasDestinationAccess;
                try
                {
                    next = OpenExistingDirectory(
                        parent,
                        component,
                        parentHasDestinationAccess
                            ? DestinationDirectoryAccess
                            : DirectoryTraversalAccess,
                        description);
                }
                catch (IOException exception) when (IsPathNotFound(exception))
                {
                    if (!parentHasDestinationAccess && handles.Count > 1)
                    {
                        parent = OpenExistingDirectory(
                            handles[^2],
                            components[handles.Count - 2],
                            DestinationDirectoryAccess,
                            description);
                        handles[^1].Dispose();
                        handles[^1] = parent;
                        hasDestinationAccess[^1] = true;
                    }
                    else if (!parentHasDestinationAccess)
                    {
                        parent = OpenDirectoryPath(
                            rootPath,
                            $"{description} root",
                            DestinationDirectoryAccess);
                        handles[^1].Dispose();
                        handles[^1] = parent;
                        hasDestinationAccess[^1] = true;
                    }

                    try
                    {
                        next = CreateRelativeDirectory(parent, component);
                        EnsureNotReparsePoint(next, $"{description} directory");
                        nextHasDestinationAccess = true;
                    }
                    catch (IOException collision) when (IsAlreadyExists(collision))
                    {
                        next = OpenExistingDirectory(
                            parent,
                            component,
                            DestinationDirectoryAccess,
                            description);
                        nextHasDestinationAccess = true;
                    }
                }

                handles.Add(next);
                hasDestinationAccess.Add(nextHasDestinationAccess);
            }

            if (!hasDestinationAccess[^1] && handles.Count > 1)
            {
                var final = OpenExistingDirectory(
                    handles[^2],
                    components[^1],
                    DestinationDirectoryAccess,
                    description);
                handles[^1].Dispose();
                handles[^1] = final;
            }

            var result = handles[^1];
            for (var index = 0; index < handles.Count - 1; index++)
            {
                handles[index].Dispose();
            }

            return result;
        }
        catch
        {
            foreach (var handle in handles)
            {
                handle.Dispose();
            }

            throw;
        }
    }

    private static SafeFileHandle OpenExistingDirectory(
        SafeFileHandle parent,
        string name,
        uint desiredAccess,
        string description)
    {
        var handle = OpenRelativeDirectory(
            parent,
            name,
            desiredAccess,
            $"{description} directory");
        try
        {
            EnsureNotReparsePoint(handle, $"{description} directory");
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static SafeFileHandle OpenDirectoryPath(
        string path,
        string description,
        uint desiredAccess = DirectoryTraversalAccess)
    {
        var handle = CreateFileW(
            path,
            desiredAccess,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            Win32OpenExisting,
            FileFlagBackupSemantics | FileOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw CreateIoException($"The {description} directory could not be opened.");
        }

        EnsureNotReparsePoint(handle, description + " directory");
        return handle;
    }

    private static SafeFileHandle OpenRelativeDirectory(
        SafeFileHandle parent,
        string name,
        uint desiredAccess = DirectoryTraversalAccess,
        string description = "source directory") =>
        OpenRelative(
            parent,
            name,
            desiredAccess,
            FileDirectoryFile | FileSynchronousIoNonalert | FileOpenReparsePoint,
            NtOpenExisting,
            description);

    private static SafeFileHandle CreateRelativeDirectory(SafeFileHandle parent, string name) =>
        OpenRelative(
            parent,
            name,
            FileListDirectory | FileAddFile | FileAddSubdirectory | FileReadAttributes | Synchronize,
            FileDirectoryFile | FileSynchronousIoNonalert | FileOpenReparsePoint,
            NtCreateNew,
            "destination directory");

    private static SafeFileHandle OpenRelativeFile(
        SafeFileHandle parent,
        string name,
        uint desiredAccess) =>
        OpenRelative(
            parent,
            name,
            desiredAccess,
            FileNonDirectoryFile | FileSynchronousIoNonalert | FileOpenReparsePoint,
            NtOpenExisting,
            "source file");

    private static SafeFileHandle CreateRelativeFile(SafeFileHandle parent, string name) =>
        OpenRelative(
            parent,
            name,
            FileReadAttributes | FileWriteData | Synchronize,
            FileNonDirectoryFile | FileSynchronousIoNonalert | FileOpenReparsePoint,
            NtCreateNew,
            "destination file");

    private static SafeFileHandle OpenRelative(
        SafeFileHandle parent,
        string name,
        uint desiredAccess,
        uint createOptions,
        uint createDisposition,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name is "." or ".."
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The relative directory entry name is invalid.", nameof(name));
        }

        var nameBuffer = Marshal.StringToHGlobalUni(name);
        var unicodeString = new UnicodeString
        {
            Length = checked((ushort)(name.Length * 2)),
            MaximumLength = checked((ushort)(name.Length * 2 + 2)),
            Buffer = nameBuffer
        };
        var unicodeStringBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        Marshal.StructureToPtr(unicodeString, unicodeStringBuffer, fDeleteOld: false);
        var objectAttributes = new ObjectAttributes
        {
            Length = Marshal.SizeOf<ObjectAttributes>(),
            RootDirectory = parent.DangerousGetHandle(),
            ObjectName = unicodeStringBuffer,
            Attributes = ObjCaseInsensitive
        };

        try
        {
            var status = NtCreateFile(
                out var rawHandle,
                desiredAccess,
                ref objectAttributes,
                out _,
                IntPtr.Zero,
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                createDisposition,
                createOptions,
                IntPtr.Zero,
                0);
            if (status < 0)
            {
                throw CreateNtIoException($"The {description} could not be opened.", status);
            }

            var handle = new SafeFileHandle(rawHandle, ownsHandle: true);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new IOException($"The {description} could not be opened.");
            }

            return handle;
        }
        finally
        {
            Marshal.FreeHGlobal(unicodeStringBuffer);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static IReadOnlyList<DirectoryEntry> EnumerateDirectory(SafeFileHandle directory)
    {
        var entries = new List<DirectoryEntry>();
        var buffer = Marshal.AllocHGlobal(64 * 1024);
        try
        {
            var restartScan = true;
            while (true)
            {
                var status = NtQueryDirectoryFile(
                    directory,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out _,
                    buffer,
                    64 * 1024,
                    FileDirectoryInformation,
                    returnSingleEntry: false,
                    IntPtr.Zero,
                    restartScan);
                restartScan = false;
                if (status == StatusNoMoreFiles || status == StatusNoSuchFile)
                {
                    break;
                }

                if (status != StatusSuccess && status != StatusBufferOverflow)
                {
                    throw CreateNtIoException("The directory could not be enumerated.", status);
                }

                var offset = 0;
                while (true)
                {
                    var nextOffset = unchecked((uint)Marshal.ReadInt32(buffer, offset));
                    var attributes = unchecked((uint)Marshal.ReadInt32(buffer, offset + 56));
                    var nameLength = unchecked((uint)Marshal.ReadInt32(buffer, offset + 60));
                    var name = Marshal.PtrToStringUni(
                        IntPtr.Add(buffer, offset + 64),
                        checked((int)nameLength / 2));
                    if (!string.IsNullOrEmpty(name) && name is not "." and not "..")
                    {
                        entries.Add(new DirectoryEntry(name, attributes));
                    }

                    if (nextOffset == 0)
                    {
                        break;
                    }

                    offset = checked(offset + (int)nextOffset);
                }
            }

            return entries;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void EnsureNotReparsePoint(SafeFileHandle handle, string description)
    {
        if (!GetFileInformationByHandle(handle, out var information))
        {
            throw CreateIoException($"The {description} attributes could not be read.");
        }

        if ((information.FileAttributes & FileAttributeReparsePoint) != 0)
        {
            throw new IOException($"The {description} is a reparse point.");
        }
    }

    private static IOException CreateIoException(string message) =>
        new(message, new Win32Exception(Marshal.GetLastWin32Error()));

    private static IOException CreateNtIoException(string message, int status)
    {
        var error = unchecked((int)RtlNtStatusToDosError(status));
        return new IOException(message, new Win32Exception(error));
    }

    private static bool IsPathNotFound(IOException exception) =>
        exception.InnerException is Win32Exception win32
        && (win32.NativeErrorCode == 2 || win32.NativeErrorCode == 3);

    private static bool IsAlreadyExists(IOException exception) =>
        exception.InnerException is Win32Exception win32
        && (win32.NativeErrorCode == 80 || win32.NativeErrorCode == 183);

    private readonly record struct DirectoryEntry(string Name, uint Attributes);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        internal ushort Length;
        internal ushort MaximumLength;
        internal IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        internal int Length;
        internal IntPtr RootDirectory;
        internal IntPtr ObjectName;
        internal uint Attributes;
        internal IntPtr SecurityDescriptor;
        internal IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out ByHandleFileInformation fileInformation);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtCreateFile(
        out IntPtr fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        IntPtr allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        IntPtr eaBuffer,
        uint eaLength);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQueryDirectoryFile(
        SafeFileHandle fileHandle,
        IntPtr @event,
        IntPtr apcRoutine,
        IntPtr apcContext,
        out IoStatusBlock ioStatusBlock,
        IntPtr fileInformation,
        uint length,
        int fileInformationClass,
        [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry,
        IntPtr fileName,
        [MarshalAs(UnmanagedType.U1)] bool restartScan);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RtlNtStatusToDosError(int status);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal IntPtr Status;
        internal IntPtr Information;
    }
}
