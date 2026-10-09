using System.Runtime.InteropServices;
using Luma.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Luma.Windows;

/// <summary>Uses installed Windows codecs; unsupported media returns a placeholder result (null).</summary>
public sealed class WindowsThumbnailGenerator : IThumbnailGenerator
{
    public async Task<byte[]?> GenerateAsync(string path, int pixels, CancellationToken ct)
    {
        if (pixels is < 32 or > 1024) throw new ArgumentOutOfRangeException(nameof(pixels));
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct);
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, (uint)pixels,
                ThumbnailOptions.UseCurrentScale).AsTask(ct);
            if (thumbnail is null || thumbnail.Type != ThumbnailType.Image || thumbnail.Size == 0 || thumbnail.Size > 16 * 1024 * 1024)
                return null;
            using var reader = new DataReader(thumbnail.GetInputStreamAt(0));
            var size = checked((uint)thumbnail.Size);
            if (await reader.LoadAsync(size).AsTask(ct) != size) return null;
            var bytes = new byte[checked((int)size)];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException)
        { return null; }
    }
}
