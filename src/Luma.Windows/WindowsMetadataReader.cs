using Luma.Core;
using Windows.Storage;

namespace Luma.Windows;

public sealed class WindowsMetadataReader : IMetadataReader
{
    public async Task<MediaMetadata> ReadAsync(string path, string kind, CancellationToken ct)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct);
            if (kind == "video")
            {
                var video = await file.Properties.GetVideoPropertiesAsync().AsTask(ct);
                // Encoding dates are not capture dates. Keep modification fallback for videos.
                return new(null, "Video: modification date fallback", (int)video.Width, (int)video.Height, video.Duration.TotalSeconds, (int)video.Orientation);
            }
            var photo = await file.Properties.GetImagePropertiesAsync().AsTask(ct);
            long? taken = photo.DateTaken.Year >= 1900 && photo.DateTaken <= DateTimeOffset.UtcNow.AddDays(2) ? photo.DateTaken.UtcTicks : null;
            return new(taken, taken is null ? "Modification date fallback" : "Windows date taken; original timezone not verified",
                (int)photo.Width, (int)photo.Height, 0, (int)photo.Orientation);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or ArgumentException)
        { return new(null, "Metadata unavailable; modification date fallback"); }
    }
}
