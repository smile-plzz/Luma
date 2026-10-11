using Microsoft.Data.Sqlite;

namespace Luma.Core;

public sealed record IndexingCheckpoint(string SourceId, long MediaCount, long CompletedFolders);

public sealed partial class MediaCache
{
    private const string ScanSchema = """
        CREATE TABLE IF NOT EXISTS scan_jobs(source_id TEXT PRIMARY KEY);
        CREATE TABLE IF NOT EXISTS scan_folders(source_id TEXT NOT NULL,path TEXT NOT NULL,done INTEGER NOT NULL DEFAULT 0,stamp INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(source_id,path));
        CREATE TABLE IF NOT EXISTS scan_media(source_id TEXT NOT NULL,relative_path TEXT NOT NULL,folder TEXT NOT NULL,length INTEGER NOT NULL,modified_ticks INTEGER NOT NULL,kind TEXT NOT NULL,PRIMARY KEY(source_id,relative_path));
        CREATE INDEX IF NOT EXISTS ix_scan_media_folder ON scan_media(source_id,folder);
        """;
    private const string ClearScan = "DELETE FROM scan_media WHERE source_id=$id; DELETE FROM scan_folders WHERE source_id=$id; DELETE FROM scan_jobs WHERE source_id=$id;";

    public async Task<IReadOnlyList<IndexingCheckpoint>> IndexingCheckpointsAsync(CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT source_id,(SELECT COUNT(*) FROM scan_media m WHERE m.source_id=j.source_id),(SELECT COUNT(*) FROM scan_folders f WHERE f.source_id=j.source_id AND done=1) FROM scan_jobs j";
        var result = new List<IndexingCheckpoint>(); await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0),reader.GetInt64(1),reader.GetInt64(2)));
        return result;
    }

    // The OS releases this lease after a crash. Staging and the published catalog are separate.
    private FileStream IndexingLease() => new(Path.GetFullPath(databasePath) + ".indexing.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    public async Task DiscardIndexingAsync(string sourceId, CancellationToken ct = default)
    {
        using var lease = IndexingLease();
        await ExecuteAsync(ClearScan,ct,("$id",sourceId));
    }

    // Completed folders are skipped; an interrupted folder re-enumerates names but reuses
    // committed file records. Every 100 files are checkpointed, including after a crash.
    public async Task<int> ScanAsync(string sourceId, string root, CancellationToken ct = default, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Source ID required",nameof(sourceId));
        ct.ThrowIfCancellationRequested();
        using var lease = IndexingLease();
        if (sourceResolver is not null) root = sourceResolver.ResolveRoot(sourceId) ?? throw new IOException("Source is offline or its identity has changed.");
        root = Path.GetFullPath(root);
        void VerifySource()
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(root) || (sourceResolver is not null && !string.Equals(sourceResolver.ResolveRoot(sourceId),root,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                throw new IOException("Indexing paused: reconnect the same drive and choose Resume indexing.");
        }
        VerifySource();
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        async Task<long> Sql(string sql, SqliteTransaction? tx = null, params (string Name, object Value)[] values)
        {
            await using var cmd = db.CreateCommand(); cmd.Transaction=tx; cmd.CommandText=sql;
            cmd.Parameters.AddWithValue("$id",sourceId);
            foreach (var v in values) cmd.Parameters.AddWithValue(v.Name,v.Value);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        }
        // Only directory attributes are checked here, never completed folders' files.
        // If a folder changed while paused, start a fresh snapshot rather than prune from stale evidence.
        var completed = new List<(string Path,long Stamp)>();
        await using (var cmd=db.CreateCommand())
        {
            cmd.CommandText="SELECT path,stamp FROM scan_folders WHERE source_id=$id AND done=1"; cmd.Parameters.AddWithValue("$id",sourceId);
            await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct)) completed.Add((r.GetString(0),r.GetInt64(1)));
        }
        if (completed.Any(f => !Directory.Exists(Path.Combine(root,f.Path)) || new DirectoryInfo(Path.Combine(root,f.Path)).Attributes.HasFlag(FileAttributes.ReparsePoint) || Directory.GetLastWriteTimeUtc(Path.Combine(root,f.Path)).Ticks != f.Stamp))
            await Sql(ClearScan);
        await using(var tx=db.BeginTransaction())
        {
            await Sql("INSERT OR IGNORE INTO sources VALUES($id,$root,$seen); INSERT OR IGNORE INTO scan_jobs VALUES($id); INSERT OR IGNORE INTO scan_folders(source_id,path) VALUES($id,'');",tx,
                ("$root",root),("$seen",DateTime.UtcNow.ToString("O")));
            await tx.CommitAsync(ct);
        }
        while(true)
        {
            VerifySource();
            string? folder;
            await using(var cmd=db.CreateCommand())
            {
                cmd.CommandText="SELECT path FROM scan_folders WHERE source_id=$id AND done=0 ORDER BY path LIMIT 1"; cmd.Parameters.AddWithValue("$id",sourceId);
                folder=await cmd.ExecuteScalarAsync(ct) as string;
            }
            if(folder is null) break;
            var directory=Path.Combine(root,folder);
            if(new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Indexing paused: a queued folder became a link. Restart indexing to exclude it.");
            var stamp=Directory.GetLastWriteTimeUtc(directory).Ticks;
            var savedStamp=await Sql("SELECT stamp FROM scan_folders WHERE source_id=$id AND path=$folder",null,("$folder",folder));
            if(savedStamp!=stamp)
            {
                await using var tx=db.BeginTransaction();
                await Sql("DELETE FROM scan_media WHERE source_id=$id AND folder=$folder; UPDATE scan_folders SET stamp=$stamp WHERE source_id=$id AND path=$folder",tx,("$folder",folder),("$stamp",stamp));
                await tx.CommitAsync(ct);
            }
            var savedPaths=new HashSet<string>(StringComparer.Ordinal);
            await using(var cmd=db.CreateCommand())
            {
                cmd.CommandText="SELECT relative_path FROM scan_media WHERE source_id=$id AND folder=$folder";
                cmd.Parameters.AddWithValue("$id",sourceId); cmd.Parameters.AddWithValue("$folder",folder);
                await using var r=await cmd.ExecuteReaderAsync(ct);
                while(await r.ReadAsync(ct)) savedPaths.Add(r.GetString(0));
            }
            var count=checked((int)await Sql("SELECT COUNT(*) FROM scan_media WHERE source_id=$id"));
            var batch=new List<FileInfo>(100);
            async Task SaveBatch()
            {
                VerifySource();
                await using var tx=db.BeginTransaction();
                foreach(var info in batch)
                    await Sql("INSERT OR REPLACE INTO scan_media VALUES($id,$path,$folder,$len,$ticks,$kind)",tx,
                        ("$path",Path.GetRelativePath(root,info.FullName)),("$folder",folder),("$len",info.Length),("$ticks",info.LastWriteTimeUtc.Ticks),("$kind",Videos.Contains(info.Extension) ? "video" : "photo"));
                await tx.CommitAsync(ct); count+=batch.Count; batch.Clear(); progress?.Report(count);
            }
            foreach(var file in Directory.EnumerateFiles(directory))
            {
                ct.ThrowIfCancellationRequested();
                if(!Extensions.Contains(Path.GetExtension(file)) || savedPaths.Contains(Path.GetRelativePath(root,file))) continue;
                var info=new FileInfo(file);
                if(info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                batch.Add(info);
                if(batch.Count==100) await SaveBatch();
            }
            if(batch.Count>0) await SaveBatch();
            VerifySource();
            await using(var tx=db.BeginTransaction())
            {
                foreach(var sub in Directory.EnumerateDirectories(directory))
                {
                    ct.ThrowIfCancellationRequested();
                    if(!new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint))
                        await Sql("INSERT OR IGNORE INTO scan_folders(source_id,path) VALUES($id,$path)",tx,("$path",Path.GetRelativePath(root,sub)));
                }
                if(Directory.GetLastWriteTimeUtc(directory).Ticks != stamp)
                    throw new IOException("Folder contents changed during indexing. Choose Resume indexing to retry.");
                await Sql("UPDATE scan_folders SET done=1,stamp=$stamp WHERE source_id=$id AND path=$path",tx,("$stamp",stamp),("$path",folder));
                await tx.CommitAsync(ct);
            }
        }
        VerifySource();
        var total=checked((int)await Sql("SELECT COUNT(*) FROM scan_media WHERE source_id=$id"));
        await using(var tx=db.BeginTransaction())
        {
            await Sql("""
                INSERT INTO media SELECT source_id,relative_path,length,modified_ticks,kind FROM scan_media WHERE source_id=$id
                ON CONFLICT(source_id,relative_path) DO UPDATE SET length=excluded.length,modified_ticks=excluded.modified_ticks,kind=excluded.kind;
                DELETE FROM media WHERE source_id=$id AND relative_path NOT IN(SELECT relative_path FROM scan_media WHERE source_id=$id);
                INSERT OR IGNORE INTO media_ids SELECT lower(hex(randomblob(16))),source_id,relative_path FROM media WHERE source_id=$id;
                UPDATE sources SET root_path=$root,last_seen=$seen WHERE id=$id;
                """ + ClearScan,tx,("$root",root),("$seen",DateTime.UtcNow.ToString("O")));
            VerifySource(); await tx.CommitAsync(ct);
        }
        progress?.Report(total); return total;
    }
}
