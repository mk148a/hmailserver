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
    private const uint FileReadAttributes = 0x00000080;
    private const uint Synchronize = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const int FileRenameInformation = 10;
    private const int StatusSuccess = 0;

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

        using var sourceHandle = WindowsHandleRelativeDirectoryCopier.OpenExistingDirectoryPath(
            fullSourcePath,
            "The restore source directory could not be opened for a bounded move.",
            Delete | FileReadAttributes | Synchronize);
        using var destinationParentHandle = WindowsHandleRelativeDirectoryCopier.OpenExistingDirectoryPath(
            destinationParentPath,
            "The restore destination parent directory could not be opened for a bounded move.",
            FileAddSubdirectory | FileReadAttributes | Synchronize);
        var fileName = System.Text.Encoding.Unicode.GetBytes(destinationName);
        var fileNameLength = fileName.Length;
        const int rootDirectoryOffset = 8;
        var fileNameLengthOffset = rootDirectoryOffset + IntPtr.Size;
        var fileNameOffset = fileNameLengthOffset + sizeof(uint);
        var informationSize = checked(fileNameOffset + fileName.Length);
        var information = Marshal.AllocHGlobal(informationSize);
        try
        {
            Marshal.Copy(new byte[informationSize], 0, information, informationSize);
            Marshal.WriteIntPtr(
                information,
                rootDirectoryOffset,
                destinationParentHandle.DangerousGetHandle());
            Marshal.WriteInt32(information, fileNameLengthOffset, fileNameLength);
            Marshal.Copy(fileName, 0, IntPtr.Add(information, fileNameOffset), fileName.Length);

            var status = NtSetInformationFile(
                    sourceHandle,
                    out _,
                    information,
                    checked((uint)informationSize),
                    FileRenameInformation);
            if (status != StatusSuccess)
            {
                throw CreateNtStatusIoException(
                    "The restore directory could not be moved with a bounded Windows rename.",
                    status);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(information);
        }
    }

    private static IOException CreateNtStatusIoException(string message, int status) =>
        new(message, new Win32Exception(unchecked((int)RtlNtStatusToDosError(status))));

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

    [DllImport("ntdll.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtSetInformationFile(
        SafeFileHandle fileHandle,
        out IoStatusBlock ioStatusBlock,
        IntPtr fileInformation,
        uint bufferSize,
        int fileInformationClass);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RtlNtStatusToDosError(int status);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }
}
