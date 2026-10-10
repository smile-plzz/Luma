using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Luma.Core;

public sealed record CachedMedia(string SourceId, string RelativePath, long Length, long ModifiedTicks, string Kind, bool IsAvailable);
public sealed record SourceInfo(string Id, string RootPath, DateTime LastSeenUtc, bool IsOnline);

/// <summary>Persistent metadata for removable media. Database lives on the PC, never on the drive.</summary>
public sealed partial class MediaCache(string databasePath, ISourceResolver? sourceResolver = null)
{
    private readonly string connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        await using var db = new SqliteConnection(connectionString);
        await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS sources(id TEXT PRIMARY KEY, root_path TEXT NOT NULL, last_seen TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS media(source_id TEXT NOT NULL, relative_path TEXT NOT NULL,
                length INTEGER NOT NULL, modified_ticks INTEGER NOT NULL, kind TEXT NOT NULL,
                PRIMARY KEY(source_id, relative_path),
                FOREIGN KEY(source_id) REFERENCES sources(id));
            CREATE INDEX IF NOT EXISTS ix_media_source ON media(source_id);
            CREATE INDEX IF NOT EXISTS ix_media_modified ON media(modified_ticks DESC,source_id,relative_path);
            CREATE INDEX IF NOT EXISTS ix_media_length ON media(length DESC,source_id,relative_path);
            CREATE TABLE IF NOT EXISTS annotations(source_id TEXT NOT NULL, relative_path TEXT NOT NULL,
                favorite INTEGER NOT NULL DEFAULT 0, tags TEXT NOT NULL DEFAULT '', PRIMARY KEY(source_id,relative_path));
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText = OrganizationSchema; await cmd.ExecuteNonQueryAsync(ct);
    }

    // Caller supplies a durable source ID (e.g. volume GUID), not a mutable drive letter.
    public async Task<int> ScanAsync(string sourceId, string root, CancellationToken ct = default, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Source ID required", nameof(sourceId));
        if (sourceResolver is not null)
            root = sourceResolver.ResolveRoot(sourceId) ?? throw new IOException("Source is offline or its identity has changed.");
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        await using var db = new SqliteConnection(connectionString);
        await db.OpenAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await using (var source = db.CreateCommand())
        {
            source.Transaction = (SqliteTransaction)transaction;
            source.CommandText = "INSERT INTO sources VALUES($id,$root,$seen) ON CONFLICT(id) DO UPDATE SET root_path=$root,last_seen=$seen";
            source.Parameters.AddWithValue("$id", sourceId);
            source.Parameters.AddWithValue("$root", root);
            source.Parameters.AddWithValue("$seen", DateTime.UtcNow.ToString("O"));
            await source.ExecuteNonQueryAsync(ct);
        }
        await using (var seen = db.CreateCommand())
        {
            seen.Transaction = (SqliteTransaction)transaction;
            seen.CommandText = "DROP TABLE IF EXISTS temp.scanned; CREATE TEMP TABLE scanned(path TEXT PRIMARY KEY)";
            await seen.ExecuteNonQueryAsync(ct);
        }
        bool complete = true;
        int count = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    try { if (!new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint)) pending.Push(sub); }
                    catch (IOException) { complete = false; } catch (UnauthorizedAccessException) { complete = false; }
                }
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    ct.ThrowIfCancellationRequested();
                    var ext = Path.GetExtension(file);
                    if (!Extensions.Contains(ext)) continue;
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                        await using var cmd = db.CreateCommand();
                        cmd.Transaction = (SqliteTransaction)transaction;
                        cmd.CommandText = """
                            INSERT INTO media VALUES($id,$path,$len,$ticks,$kind)
                            ON CONFLICT(source_id,relative_path) DO UPDATE SET
                            length=$len,modified_ticks=$ticks,kind=$kind
                            """;
                        cmd.Parameters.AddWithValue("$id", sourceId);
                        cmd.Parameters.AddWithValue("$path", Path.GetRelativePath(root, info.FullName));
                        cmd.Parameters.AddWithValue("$len", info.Length);
                        cmd.Parameters.AddWithValue("$ticks", info.LastWriteTimeUtc.Ticks);
                        cmd.Parameters.AddWithValue("$kind", Videos.Contains(ext) ? "video" : "photo");
                        await cmd.ExecuteNonQueryAsync(ct);
                        await using var seen = db.CreateCommand();
                        seen.Transaction = (SqliteTransaction)transaction;
                        seen.CommandText = "INSERT OR IGNORE INTO scanned VALUES($path)";
                        seen.Parameters.AddWithValue("$path", Path.GetRelativePath(root, info.FullName));
                        await seen.ExecuteNonQueryAsync(ct);
                        count++;
                        if (count % 100 == 0) progress?.Report(count);
                    }
                    catch (IOException) { complete = false; } catch (UnauthorizedAccessException) { complete = false; }
                }
            }
            catch (IOException) { complete = false; } catch (UnauthorizedAccessException) { complete = false; }
        }
        ct.ThrowIfCancellationRequested();
        if (!Directory.Exists(root) || (sourceResolver is not null &&
            !string.Equals(sourceResolver.ResolveRoot(sourceId), root, OperatingSystem.IsWindows() ?
                StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            throw new IOException("Source disconnected or changed during scan.");
        if (complete)
        {
            await using var reconcile = db.CreateCommand();
            reconcile.Transaction = (SqliteTransaction)transaction;
            reconcile.CommandText = "DELETE FROM media WHERE source_id=$id AND relative_path NOT IN (SELECT path FROM scanned)";
            reconcile.Parameters.AddWithValue("$id", sourceId);
            await reconcile.ExecuteNonQueryAsync(ct);
        }
        await using (var identities = db.CreateCommand())
        {
            identities.Transaction = (SqliteTransaction)transaction;
            identities.CommandText = "INSERT OR IGNORE INTO media_ids SELECT lower(hex(randomblob(16))),source_id,relative_path FROM media";
            await identities.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        progress?.Report(count);
        return count;
    }

    public async Task<IReadOnlyList<CachedMedia>> ListAsync(string sourceId, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString);
        await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT relative_path,length,modified_ticks,kind FROM media WHERE source_id=$id ORDER BY relative_path";
        cmd.Parameters.AddWithValue("$id", sourceId);
        var result = new List<CachedMedia>();
        var root = sourceResolver?.ResolveRoot(sourceId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new(sourceId, reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3), root is not null && File.Exists(SourcePaths.Combine(root, reader.GetString(0)))));
        return result;
    }

    public static string ThumbnailKey(CachedMedia media)
    {
        var data = Encoding.UTF8.GetBytes($"{media.SourceId}\n{media.RelativePath}\n{media.Length}\n{media.ModifiedTicks}");
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".mts", ".m2ts", ".3gp", ".hevc" };
    private static readonly HashSet<string> Extensions = new(Videos.Concat(new[]
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".avif", ".svg", ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2" }), StringComparer.OrdinalIgnoreCase);
}
