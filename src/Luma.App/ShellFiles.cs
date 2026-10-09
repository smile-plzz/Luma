using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Luma.App;
internal static class ShellFiles
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Operation
    {
        public nint Window; public uint Function;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool Aborted;
        public nint NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref Operation operation);
    public static bool Run(nint owner, uint function, IEnumerable<string> paths, string? destination = null)
    {
        var files = paths.ToArray();
        if (files.Length == 0) return false;
        var operation = new Operation { Window = owner, Function = function,
            From = string.Join('\0', files) + "\0\0", To = destination is null ? null : destination + "\0\0",
            Flags = 0x0040 | 0x0200 }; // Allow Windows undo and use native collision/progress/confirmation dialogs.
        var result = SHFileOperation(ref operation);
        if (result != 0 && !operation.Aborted) throw new Win32Exception(result, "Windows could not complete the file operation.");
        return !operation.Aborted;
    }
}
