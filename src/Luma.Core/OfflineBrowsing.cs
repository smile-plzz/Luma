using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace Luma.Core;

public sealed record CachedFolder(string RelativePath, string Name, long MediaCount);
public sealed record OfflineCoverage(long Cached, long Total, long CacheBytes, long CacheLimitBytes);

public sealed partial class MediaCache
{
    // Derive folders from the catalog, including Phase 1 databases. Never enumerate the drive.
    public async Task<IReadOnlyList<CachedFolder>> FoldersAsync(string sourceId, string parent = "", CancellationToken ct = default)
    {
        var prefix = parent.Length == 0 ? "" : parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            WITH descendants AS (
                SELECT substr(relative_path,length($prefix)+1) AS tail FROM media
                WHERE source_id=$id AND substr(relative_path,1,length($prefix))=$prefix
            )
            SELECT substr(tail,1,instr(tail,$separator)-1),COUNT(*) FROM descendants
            WHERE instr(tail,$separator)>0 GROUP BY 1 ORDER BY 1 COLLATE NOCASE
            """;
        cmd.Parameters.AddWithValue("$id", sourceId); cmd.Parameters.AddWithValue("$prefix", prefix);
        cmd.Parameters.AddWithValue("$separator", Path.DirectorySeparatorChar.ToString());
        var folders = new List<CachedFolder>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            folders.Add(new(prefix + reader.GetString(0), reader.GetString(0), reader.GetInt64(1)));
        return folders;
    }

    // A single SQLite reader gives a stable snapshot without loading all media into memory.
    public async IAsyncEnumerable<CachedMedia> EnumerateCachedAsync(string? sourceId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT source_id,relative_path,length,modified_ticks,kind FROM media WHERE $id IS NULL OR source_id=$id ORDER BY source_id,relative_path";
        cmd.Parameters.AddWithValue("$id", (object?)sourceId ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            yield return new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4), false);
    }
}

public sealed class OfflinePreparation(MediaCache catalog, ThumbnailCache cache, ThumbnailService thumbnails)
{
    public async Task<OfflineCoverage> InspectAsync(string? sourceId = null, CancellationToken ct = default)
    {
        var snapshot = await cache.SnapshotAsync(ct);
        long total = 0, cached = 0;
        await foreach (var media in catalog.EnumerateCachedAsync(sourceId, ct))
        {
            total++;
            if (snapshot.Keys.Contains(ThumbnailCache.Key(media))) cached++;
        }
        return new(cached, total, snapshot.Bytes, snapshot.LimitBytes);
    }

    public async Task<OfflineCoverage> PrepareAsync(string? sourceId = null,
        IProgress<long>? progress = null, CancellationToken ct = default)
    {
        long visited = 0;
        await foreach (var media in catalog.EnumerateCachedAsync(sourceId, ct))
        {
            ct.ThrowIfCancellationRequested();
            await thumbnails.GetAsync(media, ct: ct, allowEviction: false);
            progress?.Report(++visited);
        }
        // Count retained versions, not successful decodes. A browsing request may have evicted entries.
        return await InspectAsync(sourceId, ct);
    }
}
