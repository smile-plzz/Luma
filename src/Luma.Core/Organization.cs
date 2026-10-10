using Microsoft.Data.Sqlite;

namespace Luma.Core;

public sealed record Album(string Id, string Name, long Count)
{
    public override string ToString() => $"{Name} ({Count:N0})";
}
public sealed record MediaMetadata(long? TakenTicks = null, string DateOrigin = "Modification date fallback",
    int Width = 0, int Height = 0, double DurationSeconds = 0, int Orientation = 0);
public interface IMetadataReader
{
    Task<MediaMetadata> ReadAsync(string path, string kind, CancellationToken ct);
}

public sealed partial class MediaCache
{
    private const string OrganizationSchema = """
        CREATE TABLE IF NOT EXISTS media_ids(id TEXT PRIMARY KEY, source_id TEXT NOT NULL, relative_path TEXT NOT NULL, UNIQUE(source_id,relative_path));
        INSERT OR IGNORE INTO media_ids SELECT lower(hex(randomblob(16))),source_id,relative_path FROM media;
        CREATE TABLE IF NOT EXISTS albums(id TEXT PRIMARY KEY,name TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS album_items(album_id TEXT NOT NULL,media_id TEXT NOT NULL,PRIMARY KEY(album_id,media_id));
        CREATE TABLE IF NOT EXISTS metadata(source_id TEXT NOT NULL,relative_path TEXT NOT NULL,length INTEGER NOT NULL,modified_ticks INTEGER NOT NULL,
            taken_ticks INTEGER,date_origin TEXT NOT NULL,width INTEGER NOT NULL,height INTEGER NOT NULL,duration REAL NOT NULL,orientation INTEGER NOT NULL,
            PRIMARY KEY(source_id,relative_path));
        CREATE TABLE IF NOT EXISTS source_labels(source_id TEXT PRIMARY KEY,label TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_metadata_taken ON metadata(taken_ticks,source_id,relative_path);
        """;

    public async Task<IReadOnlyList<Album>> AlbumsAsync(CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT a.id,a.name,COUNT(m.source_id) FROM albums a LEFT JOIN album_items ai ON ai.album_id=a.id LEFT JOIN media_ids i ON i.id=ai.media_id LEFT JOIN media m ON m.source_id=i.source_id AND m.relative_path=i.relative_path GROUP BY a.id ORDER BY a.name COLLATE NOCASE";
        var result = new List<Album>(); await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(new(r.GetString(0), r.GetString(1), r.GetInt64(2)));
        return result;
    }

    public async Task<string> SaveAlbumAsync(string name, string? id = null, CancellationToken ct = default)
    {
        name = name.Trim(); if (name.Length is < 1 or > 100) throw new ArgumentException("Use an album name of 1–100 characters.");
        id ??= Guid.NewGuid().ToString("N");
        await ExecuteAsync("INSERT INTO albums VALUES($id,$name) ON CONFLICT(id) DO UPDATE SET name=$name", ct, ("$id", id), ("$name", name));
        return id;
    }

    public Task DeleteAlbumAsync(string id, CancellationToken ct = default) => ExecuteAsync(
        "DELETE FROM album_items WHERE album_id=$id; DELETE FROM albums WHERE id=$id", ct, ("$id", id));

    public async Task SetAlbumItemsAsync(string albumId, IEnumerable<CachedMedia> media, bool add, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        foreach (var item in media)
        {
            await using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = add ? "INSERT OR IGNORE INTO album_items SELECT $album,id FROM media_ids WHERE source_id=$source AND relative_path=$path AND EXISTS(SELECT 1 FROM albums WHERE id=$album)" :
                "DELETE FROM album_items WHERE album_id=$album AND media_id IN(SELECT id FROM media_ids WHERE source_id=$source AND relative_path=$path)";
            cmd.Parameters.AddWithValue("$album", albumId); cmd.Parameters.AddWithValue("$source", item.SourceId); cmd.Parameters.AddWithValue("$path", item.RelativePath);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public Task LabelSourceAsync(string source, string label, CancellationToken ct = default) => ExecuteAsync(
        "INSERT INTO source_labels VALUES($source,$label) ON CONFLICT(source_id) DO UPDATE SET label=$label", ct,
        ("$source", source), ("$label", label.Trim().Length > 100 ? throw new ArgumentException("Keep the name under 100 characters.") : label.Trim()));

    public async Task<Dictionary<string,string>> SourceLabelsAsync(CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT source_id,label FROM source_labels";
        var labels = new Dictionary<string,string>(); await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) labels[r.GetString(0)] = r.GetString(1);
        return labels;
    }

    public async Task<int> EnrichAsync(IMetadataReader reader, string? sourceId = null, CancellationToken ct = default, IProgress<int>? progress = null)
    {
        int count = 0;
        await foreach (var media in EnumerateCachedAsync(sourceId, ct))
        {
            ct.ThrowIfCancellationRequested();
            await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
            await using var existing = db.CreateCommand();
            existing.CommandText = "SELECT 1 FROM metadata WHERE source_id=$id AND relative_path=$path AND length=$len AND modified_ticks=$ticks";
            existing.Parameters.AddWithValue("$id", media.SourceId); existing.Parameters.AddWithValue("$path", media.RelativePath);
            existing.Parameters.AddWithValue("$len", media.Length); existing.Parameters.AddWithValue("$ticks", media.ModifiedTicks);
            if (await existing.ExecuteScalarAsync(ct) is not null) continue;
            var root = sourceResolver?.ResolveRoot(media.SourceId); if (root is null) continue;
            var path = SourcePaths.Combine(root, media.RelativePath);
            try
            {
                if (!VersionMatches(path, media)) continue;
                var metadata = await reader.ReadAsync(path, media.Kind, ct);
                if (sourceResolver?.ResolveRoot(media.SourceId) != root || !VersionMatches(path, media)) continue;
                await SaveMetadataAsync(media, metadata, ct); progress?.Report(++count);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return count;
    }

    public Task SaveMetadataAsync(CachedMedia media, MediaMetadata data, CancellationToken ct = default) => ExecuteAsync("""
        INSERT INTO metadata VALUES($id,$path,$len,$ticks,$taken,$origin,$width,$height,$duration,$orientation)
        ON CONFLICT(source_id,relative_path) DO UPDATE SET length=$len,modified_ticks=$ticks,taken_ticks=$taken,date_origin=$origin,width=$width,height=$height,duration=$duration,orientation=$orientation
        """, ct, ("$id",media.SourceId),("$path",media.RelativePath),("$len",media.Length),("$ticks",media.ModifiedTicks),
        ("$taken",(object?)data.TakenTicks ?? DBNull.Value),("$origin",data.DateOrigin),("$width",data.Width),("$height",data.Height),
        ("$duration",data.DurationSeconds),("$orientation",data.Orientation));

    // Call only after a confirmed individual filesystem move. Organization follows the stable ID.
    public async Task RelocateAsync(CachedMedia old, CachedMedia target, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO media VALUES($newid,$newpath,$len,$ticks,$kind);
            UPDATE media_ids SET source_id=$newid,relative_path=$newpath WHERE source_id=$id AND relative_path=$path;
            UPDATE annotations SET source_id=$newid,relative_path=$newpath WHERE source_id=$id AND relative_path=$path;
            UPDATE metadata SET source_id=$newid,relative_path=$newpath WHERE source_id=$id AND relative_path=$path;
            DELETE FROM media WHERE source_id=$id AND relative_path=$path;
            """;
        cmd.Parameters.AddWithValue("$id",old.SourceId); cmd.Parameters.AddWithValue("$path",old.RelativePath);
        cmd.Parameters.AddWithValue("$newid",target.SourceId); cmd.Parameters.AddWithValue("$newpath",target.RelativePath);
        cmd.Parameters.AddWithValue("$len",target.Length); cmd.Parameters.AddWithValue("$ticks",target.ModifiedTicks); cmd.Parameters.AddWithValue("$kind",target.Kind);
        await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }

    public static bool VersionMatches(string path, CachedMedia media)
    {
        var info = new FileInfo(path); return info.Exists && info.Length == media.Length && info.LastWriteTimeUtc.Ticks == media.ModifiedTicks;
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name,p.Value);
        await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }
}
