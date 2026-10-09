using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Luma.Core;

namespace Luma.Windows;

public sealed record WindowsSource(string Id, string RootPath);

/// <summary>Volume GUID + volume serial + relative folder; no marker file is written to the source.</summary>
public sealed class WindowsSourceResolver : ISourceResolver
{
    private sealed record Identity(string Volume, uint Serial, string Folder);
    private const string Prefix = "windows-volume-v1:";

    public WindowsSource Register(string folder)
    {
        folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        if (folder.StartsWith(@"\\")) throw new NotSupportedException("Choose a local or removable volume, not a network share.");
        var mount = new StringBuilder(32768);
        if (!GetVolumePathName(folder, mount, mount.Capacity)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var volume = new StringBuilder(1024);
        if (!GetVolumeNameForVolumeMountPoint(mount.ToString(), volume, volume.Capacity))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!TrySerial(volume.ToString(), out var serial)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var relative = Path.GetRelativePath(mount.ToString(), folder).ToUpperInvariant();
        // Reject junctions/symlinks in source ancestry: otherwise its files may live on a different volume.
        var current = new DirectoryInfo(folder);
        while (current is not null && !string.Equals(Path.TrimEndingDirectorySeparator(current.FullName),
            Path.TrimEndingDirectorySeparator(mount.ToString()), StringComparison.OrdinalIgnoreCase))
        {
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new NotSupportedException("Choose the actual folder instead of a symbolic link or junction.");
            current = current.Parent;
        }
        var identity = new Identity(volume.ToString(), serial, relative);
        return new(Prefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(identity)), folder);
    }

    public string? ResolveRoot(string sourceId)
    {
        if (!sourceId.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try
        {
            var identity = JsonSerializer.Deserialize<Identity>(Convert.FromBase64String(sourceId[Prefix.Length..]));
            if (identity is null || string.IsNullOrEmpty(identity.Volume) || string.IsNullOrEmpty(identity.Folder) || !identity.Volume.StartsWith(@"\\?\VOLUME{", StringComparison.OrdinalIgnoreCase)
                || !identity.Volume.EndsWith(@"}\", StringComparison.Ordinal) || identity.Volume.Length != 49 ||
                !Guid.TryParse(identity.Volume[11..^2], out var volumeGuid)) return null;
            var volumeName = $@"\\?\Volume{{{volumeGuid:D}}}\";
            if (!TrySerial(volumeName, out var serial) || serial != identity.Serial) return null;
            // Resolve the current mount dynamically: Shell/WinRT APIs do not consistently accept volume-GUID paths.
            var paths = new char[32768];
            if (!GetVolumePathNamesForVolumeName(volumeName, paths, (uint)paths.Length, out _)) return null;
            foreach (var mount in new string(paths).Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var root = identity.Folder == "." ? mount : SourcePaths.Combine(mount, identity.Folder);
                if (Directory.Exists(root)) return Path.TrimEndingDirectorySeparator(root);
            }
            return null;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        { return null; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNamesForVolumeName(string volumeName, [Out] char[] paths, uint length, out uint required);

    private static bool TrySerial(string root, out uint serial) =>
        GetVolumeInformation(root, null, 0, out serial, out _, out _, null, 0);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, int bufferLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(string rootPath, StringBuilder? volumeName, int volumeNameSize,
        out uint serialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
        StringBuilder? fileSystemName, int fileSystemNameSize);
}
