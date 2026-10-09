namespace Luma.Core;

/// <summary>Implementations must verify identity, never infer it from a previously used drive letter.</summary>
public interface ISourceResolver
{
    string? ResolveRoot(string sourceId);
}

public interface IThumbnailGenerator
{
    Task<byte[]?> GenerateAsync(string path, int pixels, CancellationToken ct);
}

public sealed class ThumbnailService(ThumbnailCache cache, ISourceResolver sources, IThumbnailGenerator generator)
{
    // Bound native decoder work and coalesce requests by rechecking after entry.
    private readonly SemaphoreSlim generationGate = new(1, 1);

    public async Task<byte[]?> GetAsync(CachedMedia media, int pixels = 256, CancellationToken ct = default)
    {
        var key = ThumbnailCache.Key(media, pixels);
        var cached = await cache.ReadAsync(key, ct);
        if (cached is not null) return cached; // Offline browsing never probes the removable drive on a hit.
        await generationGate.WaitAsync(ct);
        try
        {
            cached = await cache.ReadAsync(key, ct);
            if (cached is not null) return cached;
            var root = sources.ResolveRoot(media.SourceId);
            if (root is null) return null;
            var path = SourcePaths.Combine(root, media.RelativePath);
            try
            {
                if (!Matches(path, media)) return null;
                var bytes = await generator.GenerateAsync(path, pixels, ct);
                // An unplug, overwrite, or mount change during generation must not poison the cache.
                if (bytes is null || bytes.Length == 0 || bytes.Length > cache.MaxEntryBytes || sources.ResolveRoot(media.SourceId) != root || !Matches(path, media)) return null;
                await cache.StoreAsync(key, bytes, ct);
                return bytes;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
        finally { generationGate.Release(); }
    }

    private static bool Matches(string path, CachedMedia media)
    {
        var file = new FileInfo(path);
        return file.Exists && file.Length == media.Length && file.LastWriteTimeUtc.Ticks == media.ModifiedTicks;
    }
}

public static class SourcePaths
{
    public static string Combine(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new ArgumentException("A relative media path is required.", nameof(relativePath));
        var prefix = Path.GetFullPath(root);
        if (!Path.EndsInDirectorySeparator(prefix)) prefix += Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(prefix, relativePath));
        if (!result.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Path escapes the registered source.", nameof(relativePath));
        return result;
    }
}
