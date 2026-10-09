using Microsoft.Data.Sqlite;

namespace Luma.Core;

public enum MediaSort { Newest, Oldest, Name, Largest }
public sealed record LibraryQuery(string? SourceId = null, string Search = "", string? Kind = null,
    bool FavoritesOnly = false, MediaSort Sort = MediaSort.Newest, int Offset = 0, int Limit = 120, string Folder = "");
public sealed record LibraryItem(CachedMedia Media, bool Favorite, string Tags);
public sealed record LibraryPage(IReadOnlyList<LibraryItem> Items, long Total);

public sealed partial class MediaCache
{
    public async Task<IReadOnlyList<SourceInfo>> SourcesAsync(CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT id,root_path,last_seen FROM sources ORDER BY root_path";
        var list = new List<SourceInfo>(); await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            list.Add(new(id, reader.GetString(1), DateTime.Parse(reader.GetString(2), null,
                System.Globalization.DateTimeStyles.RoundtripKind), sourceResolver?.ResolveRoot(id) is not null));
        }
        return list;
    }

    public async Task<LibraryPage> QueryAsync(LibraryQuery query, CancellationToken ct = default)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(query));
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        const string from = """
            FROM media m LEFT JOIN annotations a ON a.source_id=m.source_id AND a.relative_path=m.relative_path
            WHERE ($source IS NULL OR m.source_id=$source)
              AND ($kind IS NULL OR m.kind=$kind)
              AND ($favorites=0 OR a.favorite=1)
              AND ($search='' OR m.relative_path LIKE $search ESCAPE '\' OR a.tags LIKE $search ESCAPE '\')
              AND ($folder='' OR m.relative_path LIKE $folder ESCAPE '\')
            """;
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        cmd.Parameters.AddWithValue("$source", (object?)query.SourceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$kind", (object?)query.Kind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$favorites", query.FavoritesOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("$search", query.Search.Length == 0 ? "" : "%" + Escape(query.Search.Trim()) + "%");
        cmd.Parameters.AddWithValue("$folder", query.Folder.Length == 0 ? "" : Escape(query.Folder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar) + "%");
        cmd.CommandText = "SELECT COUNT(*) " + from;
        var total = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        var order = query.Sort switch { MediaSort.Oldest => "m.modified_ticks ASC", MediaSort.Name => "m.relative_path COLLATE NOCASE ASC",
            MediaSort.Largest => "m.length DESC", _ => "m.modified_ticks DESC" };
        cmd.CommandText = "SELECT m.source_id,m.relative_path,m.length,m.modified_ticks,m.kind,COALESCE(a.favorite,0),COALESCE(a.tags,'') "
            + from + " ORDER BY " + order + ",m.source_id,m.relative_path LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$limit", query.Limit); cmd.Parameters.AddWithValue("$offset", query.Offset);
        var roots = new Dictionary<string, bool>(); var items = new List<LibraryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            if (!roots.TryGetValue(id, out var online)) roots[id] = online = sourceResolver?.ResolveRoot(id) is not null;
            items.Add(new(new(id, reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4), online),
                reader.GetInt64(5) == 1, reader.GetString(6)));
        }
        return new(items, total);
    }

    public async Task AnnotateAsync(CachedMedia media, bool favorite, string tags, CancellationToken ct = default)
    {
        tags = string.Join(", ", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (tags.Length > 1000) throw new ArgumentException("Keep tags under 1,000 characters.", nameof(tags));
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO annotations VALUES($id,$path,$favorite,$tags) ON CONFLICT(source_id,relative_path) DO UPDATE SET favorite=$favorite,tags=$tags";
        cmd.Parameters.AddWithValue("$id", media.SourceId); cmd.Parameters.AddWithValue("$path", media.RelativePath);
        cmd.Parameters.AddWithValue("$favorite", favorite ? 1 : 0); cmd.Parameters.AddWithValue("$tags", tags);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveSourceAsync(string id, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = "DELETE FROM annotations WHERE source_id=$id; DELETE FROM media WHERE source_id=$id; DELETE FROM sources WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id); await cmd.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
    }
}
