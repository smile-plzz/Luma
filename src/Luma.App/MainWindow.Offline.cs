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
        FolderNavigation.Visibility = source is null ? Visibility.Collapsed : Visibility.Visible;
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

}
