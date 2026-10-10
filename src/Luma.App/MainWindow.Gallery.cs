using System.Collections.ObjectModel;
using Luma.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Luma.App;
public sealed partial class MainWindow
{
    private readonly ObservableCollection<MediaGroup> groups = new();
    private ScrollViewer? galleryScroll;
    private LibraryQuery? activeQuery;
    private GalleryCursor? cursor;
    private bool loading, exhausted;
    private int peakDecoded;
    private long? beforeTicks;
    private bool Grouped => settings.GroupIndex > 0 && settings.SortIndex is 0 or 1 or 4;
    private string? SelectedAlbum => (AlbumsBox.SelectedItem as Album)?.Id;

    private void ApplyViewSettings()
    {
        settings.SortIndex=Math.Clamp(settings.SortIndex,0,5); settings.GroupIndex=Math.Clamp(settings.GroupIndex,0,3); settings.ThumbnailSize=Math.Clamp(settings.ThumbnailSize,120,320);
        SortBox.SelectedIndex = settings.SortIndex;
        GroupBox.SelectedIndex = Math.Clamp(settings.GroupIndex,0,3);
        DescendingButton.IsChecked = settings.Descending;
        ThumbnailSlider.Value = Math.Clamp(settings.ThumbnailSize,120,320);
        NamesToggle.IsChecked = settings.ShowNames; DetailsToggle.IsChecked = settings.ShowDetails;
        FitToggle.IsChecked = settings.FitImages; CompactToggle.IsChecked = settings.Compact;
        CommonToggle.IsChecked = settings.CommonOnly; RecursiveToggle.IsChecked = settings.IncludeDescendants;
    }
    private LibraryQuery CurrentQuery() => new(SelectedSource,SearchBox.Text.Trim(),Navigation.SelectedIndex == 2 ? "photo" : Navigation.SelectedIndex == 3 ? "video" : null,
        Navigation.SelectedIndex == 4,(MediaSort)settings.SortIndex,0,120,FolderFilter.Text.Trim(),settings.Descending,SelectedAlbum,settings.IncludeDescendants,settings.CommonOnly,beforeTicks);

    private async Task Refresh()
    {
        if (!ready) return;
        queryCancellation?.Cancel(); queryCancellation = new(); var token = queryCancellation.Token;
        cursor = null; exhausted = false; loading = false;
        foreach(var card in cards) { card.Realized=false; card.Thumbnail=null; }
        cards.Clear(); groups.Clear(); activeQuery = CurrentQuery();
        if (Grouped) MediaGrid.ItemsSource = new CollectionViewSource { IsSourceGrouped=true,Source=groups }.View;
        else MediaGrid.ItemsSource = cards;
        galleryScroll?.ChangeView(null,0,null,true);
        await LoadMore(); token.ThrowIfCancellationRequested();
        await LoadFolders(token);
        var months = await Task.Run(() => catalog.MonthsAsync(activeQuery,token),token); token.ThrowIfCancellationRequested();
        var wasReady = ready; ready = false; DateJump.ItemsSource = months; DateJump.SelectedIndex = -1; ready = wasReady;
    }
    private async Task LoadMore()
    {
        if (!ready || loading || exhausted || activeQuery is null || queryCancellation is null) return;
        var generation = queryCancellation; var ct = generation.Token; var query = activeQuery with { Cursor=cursor };
        loading = true; Busy.IsActive = true;
        try
        {
            var page = await Task.Run(() => catalog.QueryAsync(query,ct),ct); ct.ThrowIfCancellationRequested();
            total = page.Total; cursor = page.Next;
            foreach(var item in page.Items)
            {
                var card = new MediaCard(item,settings) { IsCut=pendingCutIds.Contains(item.Id) }; cards.Add(card);
                if (Grouped)
                {
                    if (groups.Count == 0 || groups[^1].Key != card.Group) groups.Add(new(card.Group));
                    groups[^1].Add(card);
                }
            }
            exhausted = page.Items.Count < query.Limit || cards.Count >= total;
            EmptyState.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyTitle.Text = SearchBox.Text.Length > 0 ? "No matching moments" : "Your library starts here";
            EmptyText.Text = "Add a folder or drive, or reset your filters. Saved previews remain available offline.";
            LibrarySummary.Text = $"{total:N0} items · {(Grouped ? "Chronological gallery" : "Continuous grid")}";
            if (!scanning && !preparing) Status.Text = $"{cards.Count:N0} of {total:N0} loaded · {(exhausted ? "All results loaded" : "Scroll to explore")}";
        }
        catch(OperationCanceledException) { }
        finally { if (generation == queryCancellation) { loading=false; Busy.IsActive=scanning || preparing; } }
    }
    private static T? Descendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i); if(child is T result) return result;
            if(Descendant<T>(child) is T nested) return nested;
        }
        return null;
    }
    private void GalleryLoaded(object sender,RoutedEventArgs e)
    {
        galleryScroll = Descendant<ScrollViewer>(MediaGrid);
        if(galleryScroll is not null) galleryScroll.ViewChanged += async (_,_) =>
        {
            if(ready && galleryScroll.VerticalOffset + galleryScroll.ViewportHeight >= galleryScroll.ExtentHeight - 600) await Guard(LoadMore);
        };
    }
    private async void TileChanging(ListViewBase sender,ContainerContentChangingEventArgs e)
    {
        if(e.ItemContainer.Tag is MediaCard previous && (e.InRecycleQueue || !ReferenceEquals(previous,e.Item)))
        { previous.Realized=false; previous.Thumbnail=null; }
        e.ItemContainer.Tag = e.InRecycleQueue ? null : e.Item;
        if(e.Item is not MediaCard card) return;
        card.Realized = !e.InRecycleQueue;
        if(e.InRecycleQueue) { card.Thumbnail=null; return; }
        if(!ready || clearing || card.Loading || card.Thumbnail is not null || queryCancellation is null) return;
        card.Loading=true; var ct=queryCancellation.Token;
        try
        {
            var bytes=await UseCache(() => thumbnails!.GetAsync(card.Item.Media,256,ct));
            if(bytes is null || !card.Realized || ct.IsCancellationRequested) return;
            using var stream=new InMemoryRandomAccessStream();
            using(var writer=new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); await writer.FlushAsync(); }
            stream.Seek(0); var bitmap=new BitmapImage(); await bitmap.SetSourceAsync(stream);
            if(card.Realized && !ct.IsCancellationRequested) card.Thumbnail=bitmap;
            if(AppSettings.IsSmokeTest)
            {
                peakDecoded=Math.Max(peakDecoded,cards.Count(c=>c.Thumbnail is not null));
                File.WriteAllText(Path.Combine(AppSettings.LocalRoot,"gallery-metrics.json"),System.Text.Json.JsonSerializer.Serialize(new { Loaded=cards.Count,Decoded=cards.Count(c=>c.Thumbnail is not null),PeakDecoded=peakDecoded,ManagedBytes=GC.GetTotalMemory(false) }));
            }
        }
        catch(OperationCanceledException) { }
        catch(Exception) { /* A missing codec or corrupt preview keeps the accessible placeholder. */ }
        finally { card.Loading=false; }
    }
    private async void NavigationChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready) return; Heading.Text=Navigation.SelectedItem?.ToString() ?? "Library";
        ready=false;
        if(Navigation.SelectedIndex == 1)
        {
            settings.SortIndex=4; SortBox.SelectedIndex=4;
            if(settings.GroupIndex==0) { settings.GroupIndex=2; GroupBox.SelectedIndex=2; }
        }
        AlbumsBox.SelectedItem=null; ready=true; settings.Save(); beforeTicks=null; await Guard(Refresh);
    }
    private async void FilterChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready) return;
        if(ReferenceEquals(sender,Sources)) FolderFilter.Text="";
        settings.SortIndex=Math.Max(0,SortBox.SelectedIndex);
        if(settings.SortIndex==1) { settings.Descending=false; DescendingButton.IsChecked=false; }
        settings.Save(); beforeTicks=null; await Guard(Refresh);
    }
    private async void SearchChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready) return; searchCancellation?.Cancel(); var cts=new CancellationTokenSource(); searchCancellation=cts;
        try { await Task.Delay(300,cts.Token); beforeTicks=null; await Guard(Refresh); } catch(OperationCanceledException) { }
    }
    private async void DirectionChanged(object sender,RoutedEventArgs e)
    { if(!ready)return; settings.Descending=DescendingButton.IsChecked==true; settings.Save(); await Guard(Refresh); }
    private async void GroupChanged(object sender,SelectionChangedEventArgs e)
    { if(!ready)return; settings.GroupIndex=Math.Max(0,GroupBox.SelectedIndex); settings.Save(); await Guard(Refresh); }
    private void ThumbnailSizeChanged(object sender,RangeBaseValueChangedEventArgs e)
    { if(!ready)return; settings.ThumbnailSize=(int)e.NewValue; UpdateAppearance(); }
    private void AppearanceChanged(object sender,RoutedEventArgs e)
    {
        if(!ready)return; settings.ShowNames=NamesToggle.IsChecked==true; settings.ShowDetails=DetailsToggle.IsChecked==true;
        settings.FitImages=FitToggle.IsChecked==true; settings.Compact=CompactToggle.IsChecked==true; UpdateAppearance();
    }
    private void UpdateAppearance()
    {
        var anchor=cards.FirstOrDefault(c=>c.Realized);
        foreach(var card in cards) card.UpdateView(); settings.Save();
        if(anchor is not null) MediaGrid.ScrollIntoView(anchor,ScrollIntoViewAlignment.Leading);
    }
    private async void ContentFilterChanged(object sender,RoutedEventArgs e)
    { if(!ready)return; settings.CommonOnly=CommonToggle.IsChecked==true; settings.IncludeDescendants=RecursiveToggle.IsChecked==true; settings.Save(); await Guard(Refresh); }
    private async void JumpDate(object sender,SelectionChangedEventArgs e)
    {
        if(!ready || DateJump.SelectedItem is not string month) return;
        var date=DateTime.ParseExact(month+"-01","yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
        beforeTicks=DateTime.SpecifyKind(date.AddMonths(1),DateTimeKind.Local).ToUniversalTime().Ticks-1;
        settings.Descending=true; DescendingButton.IsChecked=true;
        if(settings.SortIndex is not (0 or 1 or 4)) { settings.SortIndex=4; var r=ready;ready=false;SortBox.SelectedIndex=4;ready=r; }
        settings.Save(); await Guard(Refresh);
    }
    private async void ResetFilters(object sender,RoutedEventArgs e)
    {
        ready=false; SearchBox.Text=""; FolderFilter.Text=""; AlbumsBox.SelectedItem=null; Sources.SelectedIndex=0; Navigation.SelectedIndex=0; ready=true;
        Heading.Text="Library"; beforeTicks=null; await Guard(Refresh);
    }
    private void InspectorChanged(object sender,RoutedEventArgs e) { if(ready) Inspector.Visibility=InspectorToggle.IsChecked==true ? Visibility.Visible : Visibility.Collapsed; }
    private void GalleryWheel(object sender,PointerRoutedEventArgs e)
    {
        if(!Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Control).HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down))return;
        ThumbnailSlider.Value=Math.Clamp(ThumbnailSlider.Value + Math.Sign(e.GetCurrentPoint(MediaGrid).Properties.MouseWheelDelta)*10,120,320); e.Handled=true;
    }
    private async void SelectAllMatching(object sender,RoutedEventArgs e) => await Guard(async () =>
    {
        if(!await Confirm("Select all matching results?",$"Load and select {total:N0} matching items. File actions will affect this full selection.","Select all"))return;
        var generation=queryCancellation; scanCancellation=new(); CancelButton.Visibility=Visibility.Visible;
        try
        {
            while(!exhausted && ready && generation==queryCancellation && !scanCancellation.IsCancellationRequested)
            { if(loading) { await Task.Delay(50); continue; } await LoadMore(); }
            if(generation!=queryCancellation || scanCancellation.IsCancellationRequested) { Notify("Full-result selection cancelled. No files were changed."); return; }
            MediaGrid.SelectAll();
        }
        finally { CancelButton.Visibility=Visibility.Collapsed; }
    },true);
}
