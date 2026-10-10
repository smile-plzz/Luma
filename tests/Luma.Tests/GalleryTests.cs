using Luma.Core;
using Microsoft.Data.Sqlite;
using Xunit;
namespace Luma.Tests;
public sealed class GalleryTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"Luma-gallery-"+Guid.NewGuid());
    private readonly MediaCache catalog;
    private readonly Resolver resolver;
    public GalleryTests() { Directory.CreateDirectory(root); resolver=new(root); catalog=new(Path.Combine(root,"catalog.db"),resolver); }
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(root,true); }
    private async Task Seed(int count=8)
    {
        for(int i=0;i<count;i++) { var path=Path.Combine(root,$"photo{i:000}.jpg"); File.WriteAllBytes(path,new byte[4+i%3]); File.SetLastWriteTimeUtc(path,new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc).AddDays(i%5)); }
        await catalog.InitializeAsync(); await catalog.ScanAsync("source",root);
    }
    [Theory]
    [InlineData(MediaSort.Newest,true)] [InlineData(MediaSort.Oldest,false)]
    [InlineData(MediaSort.Name,true)] [InlineData(MediaSort.Name,false)]
    [InlineData(MediaSort.Largest,true)] [InlineData(MediaSort.Largest,false)]
    [InlineData(MediaSort.Captured,true)] [InlineData(MediaSort.Captured,false)]
    [InlineData(MediaSort.Type,true)] [InlineData(MediaSort.Type,false)]
    public async Task CursorTraversalMatchesWholeQueryWithoutDuplicates(MediaSort sort,bool descending)
    {
        await Seed(265); var query=new LibraryQuery(Sort:sort,Descending:descending,Limit:500);
        var expected=(await catalog.QueryAsync(query)).Items.Select(x=>x.Id).ToArray();
        var ids=new List<string>(); GalleryCursor? cursor=null;
        do
        {
            var page=await catalog.QueryAsync(query with { Limit=37,Cursor=cursor });
            ids.AddRange(page.Items.Select(x=>x.Id)); cursor=page.Next;
            if(page.Items.Count<37)break;
        } while(ids.Count<500);
        Assert.Equal(265,ids.Count); Assert.Equal(expected,ids); Assert.Equal(265,ids.Distinct().Count());
    }
    [Fact] public async Task AlbumsAndAnnotationsFollowRenameAndMoveAcrossSources()
    {
        await Seed(); var item=(await catalog.QueryAsync(new(Limit:1))).Items[0];
        var album=await catalog.SaveAlbumAsync("Travel"); await catalog.SetAlbumItemsAsync(album,[item.Media],true);
        await catalog.AnnotateAsync(item.Media,true,"family");
        var destination=Path.Combine(root,"destination"); Directory.CreateDirectory(destination);
        // Register an empty second source before moving; resolver in this test points all sources at root.
        await catalog.ScanAsync("destination",root);
        var target=item.Media with { SourceId="destination",RelativePath="renamed.jpg" };
        File.Move(Path.Combine(root,item.Media.RelativePath),Path.Combine(root,target.RelativePath));
        await catalog.RelocateAsync(item.Media,target);
        var moved=Assert.Single((await catalog.QueryAsync(new(AlbumId:album))).Items);
        Assert.Equal(item.Id,moved.Id); Assert.Equal("renamed.jpg",moved.Media.RelativePath); Assert.True(moved.Favorite); Assert.Equal("family",moved.Tags);
        await catalog.DeleteAlbumAsync(album); Assert.Empty(await catalog.AlbumsAsync()); Assert.True(File.Exists(Path.Combine(root,"renamed.jpg")));
    }
    [Fact] public async Task MetadataIsVersionedAndCaptureSortingFallsBack()
    {
        await Seed(2); var items=(await catalog.QueryAsync(new(Sort:MediaSort.Name))).Items;
        var taken=new DateTime(2030,1,1,0,0,0,DateTimeKind.Utc).Ticks;
        await catalog.SaveMetadataAsync(items[0].Media,new(taken,"fixture",800,600));
        Assert.Equal(items[0].Id,(await catalog.QueryAsync(new(Sort:MediaSort.Captured))).Items[0].Id);
        File.WriteAllBytes(Path.Combine(root,items[0].Media.RelativePath),new byte[12]); await catalog.ScanAsync("source",root);
        var refreshed=(await catalog.QueryAsync(new(Sort:MediaSort.Name))).Items[0]; Assert.Null(refreshed.Metadata!.TakenTicks); Assert.Equal(0,refreshed.Metadata.Width);
    }
    [Fact] public async Task MetadataEnrichmentSkipsUnchangedVersionsAndOfflineSources()
    {
        await Seed(3); var reader=new Reader();
        Assert.Equal(3,await catalog.EnrichAsync(reader)); Assert.Equal(0,await catalog.EnrichAsync(reader)); Assert.Equal(3,reader.Calls);
        File.WriteAllBytes(Path.Combine(root,"photo000.jpg"),new byte[14]); await catalog.ScanAsync("source",root);
        Assert.Equal(1,await catalog.EnrichAsync(reader)); resolver.Online=false; Assert.Equal(0,await catalog.EnrichAsync(reader));
    }
    [Fact] public async Task FolderDepthCommonFormatsAndDateJumpRespectFilters()
    {
        await Seed(2); Directory.CreateDirectory(Path.Combine(root,"nested"));
        File.WriteAllBytes(Path.Combine(root,"nested","child.png"),new byte[4]); File.WriteAllBytes(Path.Combine(root,"raw.dng"),new byte[4]);
        await catalog.ScanAsync("source",root);
        Assert.Equal(2,(await catalog.QueryAsync(new(IncludeDescendants:false,CommonOnly:true))).Total);
        Assert.Single((await catalog.QueryAsync(new(Folder:"nested",IncludeDescendants:false))).Items);
        var before=new DateTime(2025,1,1,23,59,59,DateTimeKind.Utc).Ticks;
        Assert.Single((await catalog.QueryAsync(new(BeforeTicks:before))).Items);
        Assert.Contains("2025-01",await catalog.MonthsAsync(new()));
    }
    [Fact] public async Task InitializeIsRepeatableAndPreservesIdentityAndAlbums()
    {
        await Seed(1); var item=Assert.Single((await catalog.QueryAsync(new())).Items);
        var album=await catalog.SaveAlbumAsync("Keep"); await catalog.SetAlbumItemsAsync(album,[item.Media],true);
        await catalog.InitializeAsync(); await catalog.ScanAsync("source",root);
        Assert.Equal(item.Id,Assert.Single((await catalog.QueryAsync(new(AlbumId:album))).Items).Id);
        await catalog.RemoveSourceAsync("source"); Assert.Equal(0,Assert.Single(await catalog.AlbumsAsync()).Count);
    }
    [Fact] public async Task TransferCopiesThenMovesWithoutOverwriting()
    {
        var source=Path.Combine(root,"source.jpg"); var copy=Path.Combine(root,"copy.jpg"); var target=Path.Combine(root,"moved.jpg");
        File.WriteAllBytes(source,[1,2,3]);
        Assert.True((await FileTransfer.RunAsync(source,copy,false,()=>true)).Copied); Assert.True(File.Exists(source));
        var conflict=await FileTransfer.RunAsync(source,copy,true,()=>true); Assert.NotNull(conflict.Error); Assert.True(File.Exists(source));
        Assert.True((await FileTransfer.RunAsync(source,target,true,()=>true)).Moved); Assert.False(File.Exists(source)); Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(target));
    }
    [Fact] public async Task CancellationAndChangedSourceNeverPublishPartialFiles()
    {
        var source=Path.Combine(root,"source.jpg"); var target=Path.Combine(root,"target.jpg"); File.WriteAllBytes(source,new byte[500]);
        using var ct=new CancellationTokenSource(); ct.Cancel();
        var cancelled=await FileTransfer.RunAsync(source,target,true,()=>true,ct.Token); Assert.False(cancelled.Moved); Assert.False(File.Exists(target));
        int calls=0; var changed=await FileTransfer.RunAsync(source,target,true,()=>++calls==1);
        Assert.False(changed.Copied); Assert.True(File.Exists(source)); Assert.False(File.Exists(target)); Assert.Empty(Directory.GetFiles(root,"*.tmp"));
    }
    private sealed class Resolver(string root):ISourceResolver { public bool Online=true; public string? ResolveRoot(string id)=>Online ? root : null; }
    private sealed class Reader:IMetadataReader { public int Calls; public Task<MediaMetadata> ReadAsync(string path,string kind,CancellationToken ct) { Calls++; return Task.FromResult(new MediaMetadata(null,"fixture",640,480)); } }
}
