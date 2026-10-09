using System.Diagnostics;
using System.Text.Json;
using Luma.Core;
using Luma.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Luma.App;

public sealed partial class MainWindow : Window
{
    private readonly WindowsSourceResolver resolver = new();
    private readonly MediaCache catalog;
    private readonly AppSettings settings = AppSettings.Load();
    private ThumbnailCache? cache;
    private ThumbnailService? thumbnails;
    private CancellationTokenSource? queryCancellation, scanCancellation, searchCancellation;
    private Task thumbnailTask = Task.CompletedTask;
    private bool ready, scanning, operation;
    private string sourceSignature = "";
    private int offset;
    private long total;
    private List<MediaCard> cards = new();
    private readonly DispatcherTimer availabilityTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);
    private string? SelectedSource => (Sources.SelectedItem as SourceChoice)?.Id;
    private MediaCard? Selected => MediaGrid.SelectedItem as MediaCard;

    public MainWindow()
    {
        catalog = new(Path.Combine(AppSettings.LocalRoot, "catalog.db"), resolver);
        InitializeComponent();
        var work = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1280, work.Width - 32); var height = Math.Min(850, work.Height - 32);
        AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2, width, height));
        Closed += OnClosed;
        availabilityTimer.Tick += async (_, _) => { if (ready && !scanning && !operation) await Guard(async () => await LoadSources()); };
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ready) return;
        await Guard(async () =>
        {
            ApplyTheme();
            await Task.Run(() => catalog.InitializeAsync());
            cache = await Task.Run(() => new ThumbnailCache(Path.Combine(AppSettings.LocalRoot, "thumbnails"), Math.Clamp(settings.CacheGiB, 1, 32) * 1024L * 1024 * 1024));
            thumbnails = new(cache, resolver, new WindowsThumbnailGenerator());
            if (AppSettings.IsSmokeTest) await SeedSmokeFixtures();
            await LoadSources(); ready = true; availabilityTimer.Start(); await Refresh();
        });
    }
    private async Task SeedSmokeFixtures()
    {
        var folder = Path.Combine(AppSettings.LocalRoot, "Fixture originals"); Directory.CreateDirectory(folder);
        for (int index = 0; index < 8; index++)
        {
            using var writer = new BinaryWriter(File.Create(Path.Combine(folder, $"fixture{index:00}.bmp")));
            writer.Write((ushort)0x4D42); writer.Write(54 + 256 * 256 * 3); writer.Write(0); writer.Write(54);
            writer.Write(40); writer.Write(256); writer.Write(256); writer.Write((ushort)1); writer.Write((ushort)24);
            writer.Write(0); writer.Write(256 * 256 * 3); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            { writer.Write((byte)(80 + index * 15)); writer.Write((byte)y); writer.Write((byte)x); }
        }
        var source = resolver.Register(folder);
        await Task.Run(() => catalog.ScanAsync(source.Id, folder));
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        ready = false; availabilityTimer.Stop(); scanCancellation?.Cancel(); queryCancellation?.Cancel(); searchCancellation?.Cancel();
        try { await thumbnailTask; } catch (OperationCanceledException) { }
        cache?.Dispose();
    }
    private async Task Guard(Func<Task> action, bool exclusive = false)
    {
        if (exclusive && operation) { Notify("An operation is already running. Please wait."); return; }
        if (exclusive) operation = true;
        try { await action(); }
        catch (OperationCanceledException) { Status.Text = "Cancelled. Your saved library is intact."; }
        catch (Exception ex) { Notify(ex.Message, true); Status.Text = "Action could not be completed."; }
        finally { if (exclusive) operation = false; }
    }
    private void Notify(string text, bool error = false)
    { Notice.Message = text; Notice.Severity = error ? InfoBarSeverity.Error : InfoBarSeverity.Informational; Notice.IsOpen = true; }
    private async Task LoadSources()
    {
        var selected = SelectedSource;
        var all = await Task.Run(() => catalog.SourcesAsync());
        var signature = string.Join(";", all.Select(s => s.Id + s.IsOnline));
        var changed = signature != sourceSignature; sourceSignature = signature;
        var choices = new List<SourceChoice> { new(null, "All sources") };
        choices.AddRange(all.Select(s => new SourceChoice(s.Id, $"{(s.IsOnline ? "●" : "○")} {s.RootPath}")));
        var wasReady = ready; ready = false;
        Sources.ItemsSource = choices;
        Sources.SelectedItem = choices.FirstOrDefault(s => s.Id == selected) ?? choices[0];
        ready = wasReady;
        if (ready && changed && !scanning) await Refresh();
    }
    private async Task Refresh()
    {
        if (!ready) return;
        queryCancellation?.Cancel(); var cts = new CancellationTokenSource(); queryCancellation = cts; var ct = cts.Token;
        var nav = Navigation.SelectedIndex;
        var query = new LibraryQuery(SelectedSource, SearchBox.Text.Trim(), nav == 2 ? "photo" : nav == 3 ? "video" : null,
            nav == 4, (MediaSort)Math.Max(0, SortBox.SelectedIndex), offset, 120, FolderFilter.Text.Trim());
        Busy.IsActive = true;
        try
        {
            var page = await Task.Run(() => catalog.QueryAsync(query, ct), ct); ct.ThrowIfCancellationRequested();
            total = page.Total; cards = page.Items.Select(i => new MediaCard(i)).ToList();
            if (nav == 1)
            {
                var view = new CollectionViewSource { IsSourceGrouped = true, Source = cards.GroupBy(c => c.Month).ToList() };
                MediaGrid.ItemsSource = view.View;
            }
            else MediaGrid.ItemsSource = cards;
            EmptyState.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyTitle.Text = total == 0 && string.IsNullOrEmpty(SearchBox.Text) ? "A home for your media" : "No matches here";
            EmptyText.Text = "Add a source, rescan, or adjust your search and filters. Cached media remains browsable when a drive is offline.";
            PreviousButton.IsEnabled = offset > 0; NextButton.IsEnabled = offset + 120 < total;
            PageText.Text = $"{offset / 120 + 1} / {Math.Max(1, (total + 119) / 120)}";
            if (!scanning) Status.Text = $"{total:N0} items · {cards.Count:N0} on this page";
            thumbnailTask = LoadThumbnails(cards, ct); await thumbnailTask;
        }
        catch (OperationCanceledException) { }
        finally { if (queryCancellation == cts) Busy.IsActive = scanning; }
    }
    private async Task LoadThumbnails(IEnumerable<MediaCard> items, CancellationToken ct)
    {
        foreach (var card in items)
        {
            ct.ThrowIfCancellationRequested();
            var bytes = await Task.Run(() => thumbnails!.GetAsync(card.Item.Media, 256, ct), ct);
            if (bytes is null) continue;
            ct.ThrowIfCancellationRequested();
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); await writer.FlushAsync(); }
                stream.Seek(0); var bitmap = new BitmapImage(); await bitmap.SetSourceAsync(stream); card.Thumbnail = bitmap;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* Unsupported/corrupt cache entry stays a placeholder. */ }
        }
    }
    private async void NavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return; Heading.Text = Navigation.SelectedItem?.ToString() ?? "All media";
        if (Navigation.SelectedIndex == 1) SortBox.SelectedIndex = 0;
        offset = 0; await Guard(Refresh);
    }
    private async void FilterChanged(object sender, SelectionChangedEventArgs e) { if (ready) { offset = 0; await Guard(Refresh); } }
    private async void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!ready) return; searchCancellation?.Cancel(); var cts = new CancellationTokenSource(); searchCancellation = cts;
        try { await Task.Delay(300, cts.Token); offset = 0; await Guard(Refresh); } catch (OperationCanceledException) { }
    }
    private async void PreviousPage(object sender, RoutedEventArgs e) { offset = Math.Max(0, offset - 120); await Guard(Refresh); }
    private async void NextPage(object sender, RoutedEventArgs e) { if (offset + 120 < total) { offset += 120; await Guard(Refresh); } }
    private async Task<string?> PickFolder()
    {
        var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, Handle);
        return (await picker.PickSingleFolderAsync())?.Path;
    }
    private async void AddSource(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        if (!ready) return; var folder = await PickFolder(); if (folder is null) return;
        var source = await Task.Run(() => resolver.Register(folder)); await Scan(new[] { new SourceInfo(source.Id, source.RootPath, DateTime.UtcNow, true) });
    }, true);
    private async void Rescan(object sender, RoutedEventArgs e) => await Guard(RescanCurrent, true);
    private async Task RescanCurrent()
    {
        var sources = await Task.Run(() => catalog.SourcesAsync());
        await Scan(sources.Where(s => (SelectedSource is null || s.Id == SelectedSource) && s.IsOnline));
    }
    private async Task Scan(IEnumerable<SourceInfo> sources)
    {
        scanning = true; scanCancellation = new(); Busy.IsActive = true; CancelButton.Visibility = Visibility.Visible;
        AddSourceButton.IsEnabled = RescanButton.IsEnabled = false;
        try
        {
            int count = 0;
            foreach (var source in sources)
            {
                Status.Text = "Scanning " + source.RootPath;
                var progress = new Progress<int>(n => Status.Text = $"Indexing {n:N0} items · {source.RootPath}");
                count += await Task.Run(() => catalog.ScanAsync(source.Id, source.RootPath, scanCancellation.Token, progress));
            }
            Notify($"Scan complete: {count:N0} media files. Thumbnails are cached as you browse.");
        }
        finally
        {
            scanning = false; Busy.IsActive = false; CancelButton.Visibility = Visibility.Collapsed;
            AddSourceButton.IsEnabled = RescanButton.IsEnabled = true;
            await LoadSources(); offset = 0; await Refresh();
        }
    }
    private void CancelScan(object sender, RoutedEventArgs e) => scanCancellation?.Cancel();
    private async void RemoveSource(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        if (scanning) return; var id = SelectedSource; if (id is null) { Notify("Select a single source first."); return; }
        if (!await Confirm("Remove source?", "This removes its catalog entries and tags. Original files and cached thumbnail files are untouched.", "Remove source")) return;
        await Task.Run(() => catalog.RemoveSourceAsync(id)); await LoadSources(); offset = 0; await Refresh();
    }, true);
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var card = Selected;
        SelectionText.Text = card is null ? "Select media to see details" : $"{MediaGrid.SelectedItems.Count} selected · {card.Item.Media.RelativePath} · {card.Item.Media.Length / 1024d:N0} KB";
        TagsBox.Text = card?.Item.Tags ?? ""; FavoriteButton.Content = card?.Item.Favorite == true ? "★ Unfavorite" : "☆ Favorite";
    }
    private void GridRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var element = e.OriginalSource as FrameworkElement;
        while (element is not null && element.DataContext is not MediaCard) element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        if (element?.DataContext is MediaCard card && !MediaGrid.SelectedItems.Contains(card)) MediaGrid.SelectedItem = card;
    }
    private async void GridDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => await Guard(Open);
    private string Resolve(CachedMedia media)
    {
        var root = resolver.ResolveRoot(media.SourceId) ?? throw new IOException("This drive is offline. Reconnect it to use the original file.");
        var path = SourcePaths.Combine(root, media.RelativePath);
        if (!File.Exists(path)) throw new FileNotFoundException("This file has moved or been deleted. Rescan the source.");
        return path;
    }
    private Task Open()
    {
        if (Selected is not null) Process.Start(new ProcessStartInfo(Resolve(Selected.Item.Media)) { UseShellExecute = true });
        return Task.CompletedTask;
    }
    private async void OpenSelected(object sender, RoutedEventArgs e) => await Guard(Open);
    private async void RevealSelected(object sender, RoutedEventArgs e) => await Guard(() =>
    {
        if (Selected is not null) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Resolve(Selected.Item.Media)}\"") { UseShellExecute = true });
        return Task.CompletedTask;
    });
    private async void CopyPath(object sender, RoutedEventArgs e) => await Guard(() =>
    {
        if (Selected is not null) { var data = new DataPackage(); data.SetText(Resolve(Selected.Item.Media)); Clipboard.SetContent(data); Clipboard.Flush(); }
        return Task.CompletedTask;
    });
    private async Task Copy(bool cut)
    {
        var selected = MediaGrid.SelectedItems.Cast<MediaCard>().ToArray(); if (selected.Length == 0) return;
        var files = new List<IStorageItem>();
        foreach (var card in selected) files.Add(await StorageFile.GetFileFromPathAsync(Resolve(card.Item.Media)));
        var data = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
        data.SetStorageItems(files);
        data.SetData("Luma.FileSelection.v1", JsonSerializer.Serialize(selected.Select(c => c.Item.Media).ToArray()));
        Clipboard.SetContent(data); Clipboard.Flush();
        Notify($"{files.Count} file(s) ready to {(cut ? "move" : "copy")}. Paste into a destination folder.");
    }
    private async void CopySelected(object sender, RoutedEventArgs e) => await Guard(() => Copy(false), true);
    private async void CutSelected(object sender, RoutedEventArgs e) => await Guard(() => Copy(true), true);
    private async Task Paste()
    {
        if (scanning) { Notify("Wait for indexing to finish before moving files."); return; }
        var data = Clipboard.GetContent(); if (!data.Contains(StandardDataFormats.StorageItems)) { Notify("Copy or cut files first."); return; }
        var items = await data.GetStorageItemsAsync();
        if (items.Any(i => i is not StorageFile)) throw new NotSupportedException("Phase 1 pastes files only. Use Explorer to move folders.");
        var destination = await PickFolder(); if (destination is null) return;
        var paths = items.Select(i => i.Path).ToArray();
        if (data.Contains("Luma.FileSelection.v1"))
        {
            var descriptors = JsonSerializer.Deserialize<CachedMedia[]>((string)await data.GetDataAsync("Luma.FileSelection.v1"))
                ?? throw new IOException("Clipboard file information is unavailable. Copy the files again.");
            paths = descriptors.Select(media =>
            {
                var path = Resolve(media); var info = new FileInfo(path);
                if (info.Length != media.Length || info.LastWriteTimeUtc.Ticks != media.ModifiedTicks)
                    throw new IOException("A selected file changed. Rescan and copy it again before pasting.");
                return path;
            }).ToArray();
        }
        if (paths.Any(string.IsNullOrWhiteSpace)) throw new NotSupportedException("Paste requires local files.");
        var move = data.RequestedOperation.HasFlag(DataPackageOperation.Move);
        if (ShellFiles.Run(Handle, move ? 1u : 2u, paths, destination))
        { data.ReportOperationCompleted(move ? DataPackageOperation.Move : DataPackageOperation.Copy); if (move) Clipboard.Clear(); }
        await RescanCurrent();
    }
    private async void PasteSelected(object sender, RoutedEventArgs e) => await Guard(Paste, true);
    private async Task Delete()
    {
        if (scanning) return; var selected = MediaGrid.SelectedItems.Cast<MediaCard>().ToArray(); if (selected.Length == 0) return;
        if (!await Confirm("Delete selected originals?", $"Send {selected.Length} file(s) to the Recycle Bin. Windows may warn if this drive does not support recycling. Check its confirmation carefully.", "Continue")) return;
        ShellFiles.Run(Handle, 3, selected.Select(c => Resolve(c.Item.Media))); await RescanCurrent();
    }
    private async void DeleteSelected(object sender, RoutedEventArgs e) => await Guard(Delete, true);
    private async Task Rename()
    {
        if (scanning || Selected is null) return; var media = Selected.Item.Media;
        var name = await Prompt("Rename file", "Keep the extension to preserve its file type.", Path.GetFileName(media.RelativePath));
        if (name is null || name == Path.GetFileName(media.RelativePath)) return;
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            throw new ArgumentException("Enter a valid Windows filename.");
        var original = Resolve(media); var target = Path.Combine(Path.GetDirectoryName(original)!, name);
        File.Move(original, target); // Never overwrite a different file.
        var renamed = media with { RelativePath = Path.Combine(Path.GetDirectoryName(media.RelativePath) ?? "", name) };
        await catalog.AnnotateAsync(renamed, Selected.Item.Favorite, Selected.Item.Tags);
        await RescanCurrent();
    }
    private async void RenameSelected(object sender, RoutedEventArgs e) => await Guard(Rename, true);
    private async void ToggleFavorite(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        var selected = MediaGrid.SelectedItems.Cast<MediaCard>().ToArray(); if (selected.Length == 0) return;
        var favorite = !selected.All(c => c.Item.Favorite);
        foreach (var card in selected) await catalog.AnnotateAsync(card.Item.Media, favorite, card.Item.Tags);
        await Refresh();
    }, true);
    private async void SaveTags(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        if (Selected is null) return;
        await catalog.AnnotateAsync(Selected.Item.Media, Selected.Item.Favorite, TagsBox.Text); await Refresh();
    }, true);
    private async Task<bool> Confirm(string title, string message, string action)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = message, PrimaryButtonText = action, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async Task<string?> Prompt(string title, string description, string initial)
    {
        var input = new TextBox { Text = initial, Header = description, MinWidth = 320 };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = input, PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }
    private void ApplyTheme() => Root.RequestedTheme = Enum.TryParse<ElementTheme>(settings.Theme, out var theme) ? theme : ElementTheme.Default;
    private async void Settings(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        var theme = new ComboBox { Header = "Theme", ItemsSource = new[] { "Default", "Light", "Dark" }, SelectedItem = settings.Theme };
        var budget = new NumberBox { Header = "Thumbnail cache limit (GiB, applies next start)", Minimum = 1, Maximum = 32, Value = settings.CacheGiB, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var panel = new StackPanel { Spacing = 18, MinWidth = 380 };
        panel.Children.Add(theme); panel.Children.Add(budget); panel.Children.Add(new TextBlock { Text = "Thumbnails and metadata are saved on this PC. Old thumbnails are removed when the cache fills, including thumbnails from offline drives.", TextWrapping = TextWrapping.Wrap, MaxWidth = 400 });
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Settings", Content = panel, PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        settings.Theme = theme.SelectedItem?.ToString() ?? "Default"; settings.CacheGiB = double.IsNaN(budget.Value) ? 2 : (int)Math.Clamp(budget.Value, 1, 32);
        settings.Save(); ApplyTheme();
    }, true);
    private bool HandleShortcut(KeyboardAcceleratorInvokedEventArgs e)
    {
        if (!ready || Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Root.XamlRoot) is TextBox) return false;
        e.Handled = true; return true;
    }
    private async void CopyShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) { if (HandleShortcut(e)) await Guard(() => Copy(false), true); }
    private async void CutShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) { if (HandleShortcut(e)) await Guard(() => Copy(true), true); }
    private async void PasteShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) { if (HandleShortcut(e)) await Guard(Paste, true); }
    private async void DeleteShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) { if (HandleShortcut(e)) await Guard(Delete, true); }
    private async void RenameShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) { if (HandleShortcut(e)) await Guard(Rename, true); }
    private void SelectShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) { if (HandleShortcut(e)) MediaGrid.SelectAll(); }
}
