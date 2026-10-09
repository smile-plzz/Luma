using System.Security.Cryptography;
using System.Text;

namespace Luma.Core;

/// <summary>One owner per cache directory. Store on the internal disk; dispose after all requests finish.</summary>
public sealed class ThumbnailCache : IDisposable
{
    private readonly string directory;
    private readonly long maxBytes;
    private readonly int maxEntryBytes;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly FileStream ownership;
    private sealed record Entry(string Path, long Length, DateTime Accessed);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly SortedSet<Entry> lru = new(Comparer<Entry>.Create((a, b) =>
    {
        var time = a.Accessed.CompareTo(b.Accessed);
        return time != 0 ? time : StringComparer.Ordinal.Compare(a.Path, b.Path);
    }));
    private long retainedBytes;

    public ThumbnailCache(string directory, long maxBytes = 2L * 1024 * 1024 * 1024,
        int maxEntryBytes = 16 * 1024 * 1024)
    {
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (maxEntryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxEntryBytes));
        this.directory = Path.GetFullPath(directory);
        this.maxBytes = maxBytes;
        this.maxEntryBytes = (int)Math.Min(maxBytes, maxEntryBytes);
        Directory.CreateDirectory(this.directory);
        ownership = new FileStream(Path.Combine(this.directory, "cache.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        try
        {
            // Exclusive ownership makes crash recovery safe: no other writer has an active temporary file.
            foreach (var file in Directory.EnumerateFiles(this.directory, "*.tmp")) File.Delete(file);
            foreach (var file in new DirectoryInfo(this.directory).EnumerateFiles("*.thumb"))
                Track(file.FullName, file.Length, file.LastWriteTimeUtc);
            Trim();
        }
        catch { ownership.Dispose(); throw; }
    }

    public int MaxEntryBytes => maxEntryBytes;

    public static string Key(CachedMedia media, int pixels = 256)
    {
        if (pixels is < 32 or > 1024) throw new ArgumentOutOfRangeException(nameof(pixels));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"luma-thumbnail-v1:{pixels}:{MediaCache.ThumbnailKey(media)}"))).ToLowerInvariant();
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct = default)
    {
        var path = EntryPath(key);
        await gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path)) { Forget(path); return null; }
            var length = new FileInfo(path).Length;
            if (length == 0 || length > maxEntryBytes) { File.Delete(path); Forget(path); return null; }
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var accessed = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(path, accessed); // Persist LRU across app restarts.
            Track(path, bytes.Length, accessed);
            return bytes;
        }
        finally { gate.Release(); }
    }

    public async Task StoreAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        var path = EntryPath(key);
        if (bytes.Length == 0 || bytes.Length > maxEntryBytes)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Thumbnail exceeds the entry or cache budget.");
        await gate.WaitAsync(ct);
        var temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await output.WriteAsync(bytes, ct);
                await output.FlushAsync(ct);
                output.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            var accessed = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(path, accessed);
            Track(path, bytes.Length, accessed);
            Trim();
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { gate.Release(); }
        }
    }

    private string EntryPath(string key)
    {
        if (key.Length != 64 || key.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Expected a SHA-256 cache key.", nameof(key));
        return Path.Combine(directory, key.ToLowerInvariant() + ".thumb");
    }

    private void Forget(string path)
    {
        if (!entries.Remove(path, out var previous)) return;
        lru.Remove(previous);
        retainedBytes -= previous.Length;
    }

    private void Track(string path, long length, DateTime accessed)
    {
        Forget(path);
        var entry = new Entry(path, length, accessed);
        entries.Add(path, entry);
        lru.Add(entry);
        retainedBytes += length;
    }

    private void Trim()
    {
        while (retainedBytes > maxBytes && lru.Count > 0)
        {
            var oldest = lru.Min!;
            File.Delete(oldest.Path);
            Forget(oldest.Path);
        }
    }

    public void Dispose() { ownership.Dispose(); gate.Dispose(); }
}
