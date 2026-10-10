using Luma.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Luma.App;

public sealed partial class MainWindow
{
    // Register every cache request, including canceled page loads, before a window can close.
    private async Task<T> UseCache<T>(Func<Task<T>> action)
    {
        var task = Task.Run(action);
        cacheTasks.Add(task);
        try { return await task; }
        finally { cacheTasks.Remove(task); }
    }

    private OfflinePreparation Preparation => new(catalog, cache!, thumbnails!);

    private async Task LoadFolders(CancellationToken ct)
    {
        var source = SelectedSource; var parent = FolderFilter.Text.Trim();
        IReadOnlyList<CachedFolder> folders = source is null ? Array.Empty<CachedFolder>() : await Task.Run(() => catalog.FoldersAsync(source, parent, ct), ct);
        ct.ThrowIfCancellationRequested();
        FolderList.ItemsSource = folders;
        FolderHint.Text = source is null ? "Select a source to browse its folders." :
            folders.Count == 0 ? "No indexed subfolders here." : "Subfolders · includes nested media";
    }

    private void FolderSelected(object sender, SelectionChangedEventArgs e)
    {
        if (FolderList.SelectedItem is CachedFolder folder) FolderFilter.Text = folder.RelativePath;
    }

    private void ParentFolder(object sender, RoutedEventArgs e)
        => FolderFilter.Text = Path.GetDirectoryName(FolderFilter.Text.Trim().TrimEnd(Path.DirectorySeparatorChar)) ?? "";

    private void ShowCoverage(OfflineCoverage coverage, string? source)
    {
        if (SelectedSource != source) return;
        CoverageText.Text = $"{coverage.Cached:N0} / {coverage.Total:N0} previews saved · cache {coverage.CacheBytes / 1048576d:N1} / {coverage.CacheLimitBytes / 1048576d:N0} MiB";
    }

    private async void CheckCoverage(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        if (!ready || cache is null) return;
        var source = SelectedSource;
        var coverage = await UseCache(() => Preparation.InspectAsync(source));
        if (ready) ShowCoverage(coverage, source);
    }, true);

    private async void PrepareOffline(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        if (!ready || cache is null) return;
        var source = SelectedSource;
        preparing = true; preparationCancellation = new();
        Busy.IsActive = true; CancelButton.Visibility = Visibility.Visible; PrepareButton.IsEnabled = false;
        var progress = new Progress<long>(count => { if (ready && preparing) Status.Text = $"Preparing previews · {count:N0} checked"; });
        try
        {
            var coverage = await UseCache(() => Preparation.PrepareAsync(source, progress, preparationCancellation.Token));
            if (!ready) return;
            ShowCoverage(coverage, source);
            Notify(coverage.Total == 0 ? "No indexed media. Add or rescan a source first." :
                coverage.Cached == coverage.Total ? "All indexed previews are currently saved. Original files still require the drive. Later browsing can evict previews." :
                $"{coverage.Cached:N0} of {coverage.Total:N0} previews saved. Missing previews may need a connected drive, a rescan, a supported codec, or more cache space. Existing previews were preserved.");
        }
        catch (OperationCanceledException)
        {
            if (ready)
            {
                ShowCoverage(await UseCache(() => Preparation.InspectAsync(source)), source);
                Notify("Preparation cancelled. Saved previews remain available; run preparation again to resume.");
            }
        }
        finally
        {
            preparing = false;
            if (ready)
            {
                Busy.IsActive = false; CancelButton.Visibility = Visibility.Collapsed; PrepareButton.IsEnabled = true;
                Status.Text = "Preview preparation finished. Coverage is a point-in-time snapshot.";
            }
        }
    }, true);

    private async void ClearCache(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        if (!ready || cache is null) return;
        if (!await Confirm("Clear all saved previews?", "Offline previews for every source will be removed from this PC. Original files, tags, favorites, and the catalog stay intact. Reconnect drives to regenerate previews.", "Clear previews")) return;
        clearing = true; queryCancellation?.Cancel();
        try
        {
            await Task.WhenAll(cacheTasks.ToArray());
        }
        catch (OperationCanceledException) { }
        try
        {
            if (!ready) return;
            await UseCache(async () => { await cache.ClearAsync(); return true; });
            if (!ready) return;
            CoverageText.Text = "Saved preview cache cleared.";
            foreach (var card in cards) card.Thumbnail = null;
            queryCancellation = new();
            Notify("Saved previews cleared. Browsing connected sources will cache previews again.");
        }
        finally { clearing = false; }
    }, true);
}
