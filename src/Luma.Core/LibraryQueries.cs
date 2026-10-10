using Microsoft.Data.Sqlite;

namespace Luma.Core;

public enum MediaSort { Newest, Oldest, Name, Largest, Captured, Type }
public sealed record GalleryCursor(string Value, string SourceId, string RelativePath);
public sealed record LibraryQuery(string? SourceId = null, string Search = "", string? Kind = null,
    bool FavoritesOnly = false, MediaSort Sort = MediaSort.Newest, int Offset = 0, int Limit = 120, string Folder = "", bool? Descending = null, string? AlbumId = null, bool IncludeDescendants = true, bool CommonOnly = false, long? BeforeTicks = null, GalleryCursor? Cursor = null);
public sealed record LibraryItem(CachedMedia Media, bool Favorite, string Tags, string Id = "", MediaMetadata? Metadata = null);
public sealed record LibraryPage(IReadOnlyList<LibraryItem> Items, long Total, GalleryCursor? Next = null);

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
            var currentRoot=sourceResolver?.ResolveRoot(id);
            list.Add(new(id, currentRoot ?? reader.GetString(1), DateTime.Parse(reader.GetString(2), null,
                System.Globalization.DateTimeStyles.RoundtripKind), currentRoot is not null));
        }
        return list;
    }

    private const string GalleryFrom = """
        FROM media m
        LEFT JOIN annotations a ON a.source_id=m.source_id AND a.relative_path=m.relative_path
        LEFT JOIN media_ids i ON i.source_id=m.source_id AND i.relative_path=m.relative_path
        LEFT JOIN metadata d ON d.source_id=m.source_id AND d.relative_path=m.relative_path AND d.length=m.length AND d.modified_ticks=m.modified_ticks
        WHERE ($source IS NULL OR m.source_id=$source) AND ($kind IS NULL OR m.kind=$kind)
        AND ($favorites=0 OR a.favorite=1)
        AND ($search='' OR m.relative_path LIKE $search ESCAPE '\' OR a.tags LIKE $search ESCAPE '\')
        AND ($folder='' OR m.relative_path LIKE $folder ESCAPE '\')
        AND ($recursive=1 OR instr(substr(m.relative_path,length($prefix)+1),$separator)=0)
        AND ($album IS NULL OR EXISTS(SELECT 1 FROM album_items ai WHERE ai.media_id=i.id AND ai.album_id=$album))
        AND ($common=0 OR lower(m.relative_path) GLOB '*.jpg' OR lower(m.relative_path) GLOB '*.jpeg'
          OR lower(m.relative_path) GLOB '*.png' OR lower(m.relative_path) GLOB '*.heic' OR lower(m.relative_path) GLOB '*.heif'
          OR lower(m.relative_path) GLOB '*.mp4' OR lower(m.relative_path) GLOB '*.mov' OR lower(m.relative_path) GLOB '*.gif'
          OR lower(m.relative_path) GLOB '*.webp' OR lower(m.relative_path) GLOB '*.bmp')
        """;

    private static string OrderValue(MediaSort sort) => sort switch
    {
        MediaSort.Name => "m.relative_path COLLATE NOCASE", MediaSort.Largest => "m.length",
        MediaSort.Captured => "COALESCE(d.taken_ticks,m.modified_ticks)", MediaSort.Type => "luma_extension(m.relative_path) COLLATE NOCASE", _ => "m.modified_ticks"
    };
    private static void QueryParameters(SqliteCommand cmd, LibraryQuery query)
    {
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var prefix = query.Folder.Length == 0 ? "" : query.Folder.TrimEnd('\\','/') + Path.DirectorySeparatorChar;
        cmd.Parameters.AddWithValue("$source",(object?)query.SourceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$kind",(object?)query.Kind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$favorites",query.FavoritesOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("$search",query.Search.Trim().Length == 0 ? "" : "%" + Escape(query.Search.Trim()) + "%");
        cmd.Parameters.AddWithValue("$folder",prefix.Length == 0 ? "" : Escape(prefix) + "%");
        cmd.Parameters.AddWithValue("$prefix",prefix); cmd.Parameters.AddWithValue("$separator",Path.DirectorySeparatorChar.ToString());
        cmd.Parameters.AddWithValue("$recursive",query.IncludeDescendants ? 1 : 0);
        cmd.Parameters.AddWithValue("$album",(object?)query.AlbumId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$common",query.CommonOnly ? 1 : 0);
    }

    public async Task<LibraryPage> QueryAsync(LibraryQuery query, CancellationToken ct = default)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(query));
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        db.CreateFunction<string,string>("luma_extension",path => Path.GetExtension(path).ToLowerInvariant(),isDeterministic:true);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.Transaction = tx; QueryParameters(cmd,query);
        var value = OrderValue(query.Sort);
        var descending = query.Descending ?? query.Sort is MediaSort.Newest or MediaSort.Largest or MediaSort.Captured;
        var from = GalleryFrom;
        if (query.BeforeTicks is not null)
        {
            from += " AND " + (query.Sort == MediaSort.Captured ? OrderValue(MediaSort.Captured) : "m.modified_ticks") + " <= $before";
            cmd.Parameters.AddWithValue("$before",query.BeforeTicks.Value);
        }
        cmd.CommandText = "SELECT COUNT(*) " + from;
        var total = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        if (query.Cursor is not null)
        {
            from += $" AND ({value} {(descending ? "<" : ">")} $cursor OR ({value}=$cursor AND (m.source_id>$cs OR (m.source_id=$cs AND m.relative_path>$cp))))";
            cmd.Parameters.AddWithValue("$cursor", query.Sort is MediaSort.Name or MediaSort.Type ? query.Cursor.Value : long.Parse(query.Cursor.Value, System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$cs",query.Cursor.SourceId); cmd.Parameters.AddWithValue("$cp",query.Cursor.RelativePath);
        }
        cmd.CommandText = "SELECT m.source_id,m.relative_path,m.length,m.modified_ticks,m.kind,COALESCE(a.favorite,0),COALESCE(a.tags,''),COALESCE(i.id,''),d.taken_ticks,COALESCE(d.date_origin,'Modification date fallback'),COALESCE(d.width,0),COALESCE(d.height,0),COALESCE(d.duration,0),COALESCE(d.orientation,0)," + value + " " + from + " ORDER BY " + value + (descending ? " DESC" : " ASC") + ",m.source_id,m.relative_path LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$limit",query.Limit); cmd.Parameters.AddWithValue("$offset",query.Cursor is null ? query.Offset : 0);
        var roots = new Dictionary<string,bool>(); var items = new List<LibraryItem>(); GalleryCursor? next = null;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var id = r.GetString(0);
            if (!roots.TryGetValue(id,out var online)) roots[id] = online = sourceResolver?.ResolveRoot(id) is not null;
            items.Add(new(new(id,r.GetString(1),r.GetInt64(2),r.GetInt64(3),r.GetString(4),online),r.GetInt64(5)==1,r.GetString(6),r.GetString(7),
                new(r.IsDBNull(8) ? null : r.GetInt64(8),r.GetString(9),r.GetInt32(10),r.GetInt32(11),r.GetDouble(12),r.GetInt32(13))));
            next = new(Convert.ToString(r.GetValue(14),System.Globalization.CultureInfo.InvariantCulture)!,id,r.GetString(1));
        }
        return new(items,total,next);
    }

    public async Task<IReadOnlyList<string>> MonthsAsync(LibraryQuery query, CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); QueryParameters(cmd,query);
        var date = query.Sort == MediaSort.Captured ? OrderValue(MediaSort.Captured) : "m.modified_ticks";
        cmd.CommandText = "SELECT DISTINCT strftime('%Y-%m',(" + date + "-621355968000000000)/10000000,'unixepoch','localtime') " + GalleryFrom + " ORDER BY 1 DESC";
        var result = new List<string>(); await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) if (!r.IsDBNull(0)) result.Add(r.GetString(0));
        return result;
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
        cmd.CommandText = "DELETE FROM album_items WHERE media_id IN(SELECT id FROM media_ids WHERE source_id=$id); DELETE FROM media_ids WHERE source_id=$id; DELETE FROM metadata WHERE source_id=$id; DELETE FROM source_labels WHERE source_id=$id; DELETE FROM annotations WHERE source_id=$id; DELETE FROM media WHERE source_id=$id; DELETE FROM sources WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id); await cmd.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
    }
}
