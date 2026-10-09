using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Luma.Core;

public sealed record CachedMedia(string SourceId, string RelativePath, long Length, long ModifiedTicks, string Kind, bool IsAvailable);
public sealed record SourceInfo(string Id, string RootPath, DateTime LastSeenUtc, bool IsOnline);

/// <summary>Persistent metadata for removable media. Database lives on the PC, never on the drive.</summary>
public sealed class MediaCache(string databasePath)
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
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Caller supplies a durable source ID (e.g. volume GUID), not a mutable drive letter.
    public async Task<int> ScanAsync(string sourceId, string root, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Source ID required", nameof(sourceId));
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
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
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
                        count++;
                    }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        await transaction.CommitAsync(ct);
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
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new(sourceId, reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3), false));
        return result;
    }

    public static string ThumbnailKey(CachedMedia media)
    {
        var data = Encoding.UTF8.GetBytes($"{media.SourceId}\n{media.RelativePath}\n{media.Length}\n{media.ModifiedTicks}");
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v" };
    private static readonly HashSet<string> Extensions = new(Videos.Concat(new[]
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif" }), StringComparer.OrdinalIgnoreCase);
}
