using System.Runtime.InteropServices;
using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IFileDeleter"/>. It sends the file to the Windows Recycle Bin
/// through the shell's own file operation, the same way Explorer's Delete does, so a mistaken Delete
/// File can be undone (INV-081). The user has already confirmed the delete, so the shell asks nothing
/// more. The one exception is a file that cannot be recycled, such as one on a network share: the
/// shell warns before erasing it for good, and declining keeps the file.
/// </summary>
public sealed class RecycleBinFileDeleter : IFileDeleter
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofWantNukeWarning = 0x4000;

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Task.FromException(new FileNotFoundException("The file to delete does not exist.", fullPath));
        }

        var operation = new ShFileOpStruct
        {
            Func = FoDelete,

            // The shell takes a list of paths, each null-terminated, with an extra null to end it.
            From = fullPath + '\0',
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi | FofWantNukeWarning,
        };

        var result = SHFileOperation(ref operation);
        if (operation.AnyOperationsAborted)
        {
            return Task.FromException(new OperationCanceledException($"Deleting '{fullPath}' was cancelled."));
        }

        return result == 0
            ? Task.CompletedTask
            : Task.FromException(new IOException($"Could not delete '{fullPath}' (shell error 0x{result:X})."));
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOp);

    // SHFILEOPSTRUCTW. The default (natural) packing matches the native layout on x64.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }
}
