using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal interface IBackupRestoreDataDirectoryMutation
{
    void MoveDirectory(string sourcePath, string destinationPath);
}

[ComVisible(false)]
internal sealed class WindowsBackupRestoreDataDirectoryMutation : IBackupRestoreDataDirectoryMutation
{
    private const uint Delete = 0x00010000;
    private const uint FileAddSubdirectory = 0x00000004;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const int FileRenameInfo = 3;

    public void MoveDirectory(string sourcePath, string destinationPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The non-DB restore directory mutation is supported only on Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var destinationParentPath = Path.GetDirectoryName(fullDestinationPath);
        var destinationName = Path.GetFileName(fullDestinationPath);
        if (destinationParentPath is null
            || string.IsNullOrWhiteSpace(destinationName)
            || string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase)
            || destinationName is "." or ".."
            || destinationName.Contains(Path.DirectorySeparatorChar)
            || destinationName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The restore directory move paths are invalid.");
        }

        using var sourceHandle = OpenDirectory(
            fullSourcePath,
            Delete,
            "The restore source directory could not be opened for a bounded move.");
        using var destinationParentHandle = OpenDirectory(
            destinationParentPath,
            FileAddSubdirectory,
            "The restore destination parent directory could not be opened for a bounded move.");
        var fileName = System.Text.Encoding.Unicode.GetBytes(destinationName + "\0");
        var fileNameLength = checked(fileName.Length - sizeof(char));
        var fileNameOffset = (IntPtr.Size * 2) + sizeof(uint);
        var informationSize = checked(fileNameOffset + fileName.Length);
        var information = Marshal.AllocHGlobal(informationSize);
        try
        {
            Marshal.Copy(new byte[informationSize], 0, information, informationSize);
            Marshal.WriteIntPtr(
                information,
                IntPtr.Size,
                destinationParentHandle.DangerousGetHandle());
            Marshal.WriteInt32(information, IntPtr.Size * 2, fileNameLength);
            Marshal.Copy(fileName, 0, IntPtr.Add(information, fileNameOffset), fileName.Length);

            if (!SetFileInformationByHandle(
                    sourceHandle,
                    FileRenameInfo,
                    information,
                    checked((uint)informationSize)))
            {
                throw CreateWindowsIoException(
                    "The restore directory could not be moved with a bounded Windows rename.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(information);
        }
    }

    private static SafeFileHandle OpenDirectory(
        string path,
        uint desiredAccess,
        string failureMessage)
    {
        var handle = CreateFileW(
            path,
            desiredAccess,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw CreateWindowsIoException(failureMessage);
        }

        return handle;
    }

    private static IOException CreateWindowsIoException(string message) =>
        new(message, new Win32Exception(Marshal.GetLastWin32Error()));

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
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        IntPtr fileInformation,
        uint bufferSize);
}
