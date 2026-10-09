using Luma.Windows;
using Luma.Core;
using Xunit;

namespace Luma.Windows.Tests;

public sealed class WindowsTests
{
    [Fact]
    public async Task GeneratesAndCachesBitmapThroughVolumeIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "Luma-bitmap-" + Guid.NewGuid());
        var cacheRoot = Path.Combine(Path.GetTempPath(), "Luma-cache-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "sample.bmp");
            // A valid 256x256 uncompressed 24-bit BMP; no external fixture or codec package.
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write((ushort)0x4D42); writer.Write(54 + 256 * 256 * 3);
                writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(256); writer.Write(256);
                writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0); writer.Write(256 * 256 * 3);
                writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                writer.Write(Enumerable.Repeat((byte)120, 256 * 256 * 3).ToArray());
            }
            var resolver = new WindowsSourceResolver();
            var source = resolver.Register(root);
            var info = new FileInfo(path);
            var media = new CachedMedia(source.Id, "sample.bmp", info.Length, info.LastWriteTimeUtc.Ticks, "photo", true);
            using var cache = new ThumbnailCache(cacheRoot);
            var service = new ThumbnailService(cache, resolver, new WindowsThumbnailGenerator());
            Assert.NotNull(await service.GetAsync(media));
            File.Delete(path);
            Assert.NotNull(await service.GetAsync(media));
        }
        finally
        {
            Directory.Delete(root, true);
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    [Fact]
    public void IdentityIsStableAndFoldersOnSameVolumeAreDistinct()
    {
        var root = Path.Combine(Path.GetTempPath(), "Luma-win-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "other"));
        try
        {
            var resolver = new WindowsSourceResolver();
            var source = resolver.Register(root);
            Assert.Equal(source.Id, resolver.Register(root + Path.DirectorySeparatorChar).Id);
            Assert.NotEqual(source.Id, resolver.Register(Path.Combine(root, "other")).Id);
            var resolved = resolver.ResolveRoot(source.Id);
            Assert.NotNull(resolved);
            File.WriteAllText(Path.Combine(root, "probe.txt"), "same volume");
            Assert.Equal("same volume", File.ReadAllText(Path.Combine(resolved!, "probe.txt")));
            Assert.Null(resolver.ResolveRoot("windows-volume-v1:invalid"));
        }
        finally { Directory.Delete(root, true); }
    }
}
