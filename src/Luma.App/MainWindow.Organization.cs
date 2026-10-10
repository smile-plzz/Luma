using System.Text.Json;
using Luma.Core;
using Luma.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Luma.App;
public sealed partial class MainWindow
{
    private async Task LoadAlbums()
    {
        var id=SelectedAlbum; var albums=await catalog.AlbumsAsync(); var r=ready; ready=false;
        AlbumsBox.ItemsSource=albums; AlbumsBox.SelectedItem=albums.FirstOrDefault(a=>a.Id==id); ready=r;
    }
    private async void AlbumChanged(object sender,SelectionChangedEventArgs e)
    { if(!ready)return; Heading.Text=(AlbumsBox.SelectedItem as Album)?.Name ?? "Library"; beforeTicks=null; await Guard(Refresh); }
    private async void LeaveAlbum(object sender,RoutedEventArgs e) { AlbumsBox.SelectedItem=null; await Guard(Refresh); }
    private async void NewAlbum(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        var name=await Prompt("New album","Albums organize media without moving original files.",""); if(name is null)return;
        var id=await catalog.SaveAlbumAsync(name); await LoadAlbums(); AlbumsBox.SelectedItem=((IReadOnlyList<Album>)AlbumsBox.ItemsSource).First(a=>a.Id==id);
    },true);
    private async void RenameAlbum(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        if(AlbumsBox.SelectedItem is not Album album)return;
        var name=await Prompt("Rename album","Album name",album.Name); if(name is null)return;
        await catalog.SaveAlbumAsync(name,album.Id); await LoadAlbums(); Heading.Text=name;
    },true);
    private async void DeleteAlbum(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        if(AlbumsBox.SelectedItem is not Album album)return;
        if(!await Confirm("Delete album?","Only the album is removed. Original files, tags and favorites stay intact.","Delete album"))return;
        await catalog.DeleteAlbumAsync(album.Id); await LoadAlbums(); await Refresh();
    },true);
    private async void AddToAlbum(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        var items=MediaGrid.SelectedItems.Cast<MediaCard>().Select(c=>c.Item.Media).ToArray(); if(items.Length==0)return;
        var albums=await catalog.AlbumsAsync();
        if(albums.Count==0) { Notify("Create an album in the sidebar first.");return; }
        var picker=new ComboBox { ItemsSource=albums,SelectedIndex=0,MinWidth=280 };
        var dialog=new ContentDialog { XamlRoot=Root.XamlRoot,Title=$"Add {items.Length} items to album",Content=picker,PrimaryButtonText="Add",CloseButtonText="Cancel" };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
        await catalog.SetAlbumItemsAsync(((Album)picker.SelectedItem).Id,items,true); await LoadAlbums(); Notify("Added to album. Originals stayed in place.");
    },true);
    private async void RemoveFromAlbum(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        if(SelectedAlbum is not string id)return;
        await catalog.SetAlbumItemsAsync(id,MediaGrid.SelectedItems.Cast<MediaCard>().Select(c=>c.Item.Media).ToArray(),false);
        await LoadAlbums(); await Refresh();
    },true);
    private async void RenameSourceLabel(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        if(SelectedSource is not string id){Notify("Select a source first.");return;}
        var name=await Prompt("Name this source","A friendly label. The folder itself will not be renamed.",""); if(name is null)return;
        await catalog.LabelSourceAsync(id,name); await LoadSources();
    },true);
    private async void RefreshMetadata(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        scanning=true; scanCancellation=new(); Busy.IsActive=true; CancelButton.Visibility=Visibility.Visible;
        try
        {
            var source=SelectedSource;
            var progress=new Progress<int>(n=>Status.Text=$"Metadata · {n:N0} updated");
            var count=await Task.Run(()=>catalog.EnrichAsync(new WindowsMetadataReader(),source,scanCancellation.Token,progress));
            Notify($"Metadata refreshed for {count:N0} changed files. Unsupported dates use modification time.");
        }
        finally { scanning=false; Busy.IsActive=false; CancelButton.Visibility=Visibility.Collapsed; if(ready)await Refresh(); }
    },true);
    private void StartDrag(object sender,DragItemsStartingEventArgs e)
    {
        var media=e.Items.Cast<MediaCard>().Select(c=>c.Item.Media).ToArray();
        e.Data.SetData("Luma.FileSelection.v1",JsonSerializer.Serialize(media));
        e.Data.RequestedOperation=DataPackageOperation.Copy|DataPackageOperation.Move;
        e.Data.SetDataProvider(StandardDataFormats.StorageItems,async request=>
        {
            var deferral=request.GetDeferral();
            try
            {
                var items=new List<IStorageItem>();
                foreach(var item in media) items.Add(await StorageFile.GetFileFromPathAsync(Resolve(item)));
                request.SetData(items);
            }
            catch(Exception ex) { DispatcherQueue.TryEnqueue(()=>Notify(ex.Message,true)); }
            finally { deferral.Complete(); }
        });
    }
    private void AlbumDragOver(object sender,DragEventArgs e)
    {
        if(SelectedAlbum is not null && e.DataView.Contains("Luma.FileSelection.v1"))
        { e.AcceptedOperation=DataPackageOperation.Copy; e.DragUIOverride.Caption="Add to selected album · originals stay in place"; e.Handled=true; }
    }
    private async void AlbumDrop(object sender,DragEventArgs e)
    {
        var deferral=e.GetDeferral();
        try { await Guard(async () =>
        {
            if(SelectedAlbum is not string id || !e.DataView.Contains("Luma.FileSelection.v1"))return;
            var media=JsonSerializer.Deserialize<CachedMedia[]>((string)await e.DataView.GetDataAsync("Luma.FileSelection.v1")) ?? [];
            await catalog.SetAlbumItemsAsync(id,media,true); await LoadAlbums(); await Refresh();
        },true); }
        finally { deferral.Complete(); }
    }
    private void FolderDragOver(object sender,DragEventArgs e)
    {
        if(SelectedSource is null || !e.DataView.Contains(StandardDataFormats.StorageItems))return;
        var move=Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Shift).HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);
        e.AcceptedOperation=move ? DataPackageOperation.Move : DataPackageOperation.Copy;
        e.DragUIOverride.Caption=move ? "Move to folder · confirmation required" : "Copy to folder · hold Shift to move"; e.Handled=true;
    }
    private async void FolderDrop(object sender,DragEventArgs e)
    {
        var deferral=e.GetDeferral();
        try { await Guard(async () =>
        {
            if(SelectedSource is not string id)return;
            var root=resolver.ResolveRoot(id) ?? throw new IOException("Destination drive is offline.");
            var element=e.OriginalSource as FrameworkElement;
            while(element is not null && element.DataContext is not CachedFolder) element=Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element) as FrameworkElement;
            var folder=(element?.DataContext as CachedFolder)?.RelativePath ?? FolderFilter.Text.Trim();
            var destination=folder.Length==0 ? root : SourcePaths.Combine(root,folder);
            await TransferData(e.DataView,destination,e.AcceptedOperation==DataPackageOperation.Move);
        },true); }
        finally { deferral.Complete(); }
    }
    private async void CopyToFolder(object sender,RoutedEventArgs e) => await Guard(()=>TransferSelected(false),true);
    private async void MoveToFolder(object sender,RoutedEventArgs e) => await Guard(()=>TransferSelected(true),true);
    private async Task TransferSelected(bool move)
    {
        var destination=await PickFolder(); if(destination is null)return;
        var media=MediaGrid.SelectedItems.Cast<MediaCard>().Select(c=>c.Item.Media).ToArray();
        await TransferPaths(media.Select(Resolve).ToArray(),media,destination,move);
    }
    private async Task TransferData(DataPackageView data,string destination,bool move,bool clearClipboard=false)
    {
        var items=await data.GetStorageItemsAsync();
        if(items.Any(i=>i is not StorageFile))throw new NotSupportedException("Transfer files only. Add folders as sources.");
        CachedMedia[] media=[];
        if(data.Contains("Luma.FileSelection.v1"))media=JsonSerializer.Deserialize<CachedMedia[]>((string)await data.GetDataAsync("Luma.FileSelection.v1")) ?? [];
        var paths=media.Length>0 ? media.Select(Resolve).ToArray() : items.Select(i=>i.Path).ToArray();
        if(await TransferPaths(paths,media,destination,move))
        { data.ReportOperationCompleted(move ? DataPackageOperation.Move : DataPackageOperation.Copy); if(move && clearClipboard) { Clipboard.Clear(); pendingCutIds.Clear(); foreach(var card in cards)card.IsCut=false; } }
    }
    private async Task<bool> TransferPaths(string[] paths,CachedMedia[] media,string destination,bool move)
    {
        if(paths.Length==0)return false;
        if(!await Confirm(move ? "Move selected originals?" : "Copy selected originals?",$"{paths.Length:N0} file(s) → {destination}\\nExisting filenames will be skipped, never overwritten.",move ? "Move files" : "Copy files"))return false;
        var destinationIdentity=resolver.Register(destination);
        var roots=await catalog.SourcesAsync();
        string targetId=destinationIdentity.Id,targetRoot=destination;
        foreach(var source in roots.OrderByDescending(s=>s.RootPath.Length))
        {
            var current=resolver.ResolveRoot(source.Id); if(current is null)continue;
            var prefix=Path.TrimEndingDirectorySeparator(current)+Path.DirectorySeparatorChar;
            if(destination.Equals(current,StringComparison.OrdinalIgnoreCase) || destination.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) {targetId=source.Id;targetRoot=current;break;}
        }
        if(!roots.Any(s=>s.Id==targetId)) await Task.Run(()=>catalog.ScanAsync(targetId,targetRoot));
        scanCancellation=new(); CancelButton.Visibility=Visibility.Visible; Busy.IsActive=true;
        var reports=new List<TransferResult>(); bool catalogFailure=false;
        try
        {
            for(int index=0;index<paths.Length;index++)
            {
                if(scanCancellation.IsCancellationRequested)break;
                var path=paths[index]; var descriptor=media.Length==paths.Length ? media[index] : null;
                var original=new FileInfo(path); var length=original.Length; var ticks=original.LastWriteTimeUtc.Ticks;
                var target=Path.Combine(destination,Path.GetFileName(path));
                bool Valid() => resolver.ResolveRoot(destinationIdentity.Id) is not null && (descriptor is null ? new FileInfo(path) is var f && f.Exists && f.Length==length && f.LastWriteTimeUtc.Ticks==ticks : resolver.ResolveRoot(descriptor.SourceId) is string r && SourcePaths.Combine(r,descriptor.RelativePath).Equals(path,StringComparison.OrdinalIgnoreCase) && MediaCache.VersionMatches(path,descriptor));
                Status.Text=$"{(move ? "Moving" : "Copying")} {index+1:N0} / {paths.Length:N0}";
                var result=await Task.Run(()=>FileTransfer.RunAsync(path,target,move,Valid,scanCancellation.Token)); reports.Add(result);
                if(result.Moved && descriptor is not null)
                {
                    try
                    {
                        var info=new FileInfo(target);
                        await catalog.RelocateAsync(descriptor,descriptor with { SourceId=targetId,RelativePath=Path.GetRelativePath(targetRoot,target),Length=info.Length,ModifiedTicks=info.LastWriteTimeUtc.Ticks });
                    }
                    catch(Exception ex) { catalogFailure=true; reports[^1]=result with { Error="File moved; catalog reconciliation failed: "+ex.Message }; }
                }
            }
            var log=Path.Combine(AppSettings.LocalRoot,"last-transfer.json");
            await File.WriteAllTextAsync(log,JsonSerializer.Serialize(reports,new JsonSerializerOptions { WriteIndented=true }));
            if(!catalogFailure)
            {
                await Task.Run(()=>catalog.ScanAsync(targetId,targetRoot));
                await LoadSources(); await LoadAlbums(); await Refresh();
            }
            var errors=reports.Where(r=>r.Error is not null).ToArray();
            Notify($"{reports.Count(r=>move ? r.Moved : r.Copied):N0} / {paths.Length:N0} completed. " + (errors.Length>0 ? errors[0].Error+" Details: "+log : "Original organization is retained for in-app moves."),errors.Length>0);
            return reports.Count==paths.Length && errors.Length==0;
        }
        finally
        {
            try { await File.WriteAllTextAsync(Path.Combine(AppSettings.LocalRoot,"last-transfer.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions { WriteIndented=true })); }
            catch(IOException) { Notify("The transfer report could not be saved. Check available disk space.",true); }
            Busy.IsActive=false; CancelButton.Visibility=Visibility.Collapsed;
        }
    }
}
