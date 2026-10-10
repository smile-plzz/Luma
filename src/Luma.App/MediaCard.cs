using System.ComponentModel;
using System.Collections.ObjectModel;
using Luma.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
namespace Luma.App;
public sealed class MediaCard(LibraryItem item, AppSettings settings) : INotifyPropertyChanged
{
    public LibraryItem Item { get; } = item;
    public string Name => Path.GetFileName(Item.Media.RelativePath);
    public DateTime Date => new DateTime(settings.SortIndex == 4 ? Item.Metadata?.TakenTicks ?? Item.Media.ModifiedTicks : Item.Media.ModifiedTicks, DateTimeKind.Utc).ToLocalTime();
    public string Group => Date.ToString(settings.GroupIndex switch { 1 => "yyyy", 3 => "dddd, d MMMM yyyy", _ => "MMMM yyyy" });
    public string Caption => $"{Date:dd MMM yyyy} · {Item.Media.Length / 1024d:N0} KB";
    public string State => (Item.Favorite ? "★  " : "") + (Item.Media.Kind == "video" ? "VIDEO  " : "") + (Item.Media.IsAvailable ? "" : "Offline");
    public double TileWidth => settings.ThumbnailSize;
    public double TileHeight => settings.ThumbnailSize * 0.75;
    public Thickness Spacing => new(settings.Compact ? 2 : 5);
    public Visibility Labels => settings.ShowNames ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Details => settings.ShowDetails ? Visibility.Visible : Visibility.Collapsed;
    public Stretch Fit => settings.FitImages ? Stretch.Uniform : Stretch.UniformToFill;
    public bool Realized { get; set; }
    public bool Loading { get; set; }
    public BitmapImage? Thumbnail { get => thumbnail; set { thumbnail=value; Changed(nameof(Thumbnail)); } }
    private BitmapImage? thumbnail;
    public void UpdateView() { foreach(var name in new[]{nameof(TileWidth),nameof(TileHeight),nameof(Spacing),nameof(Labels),nameof(Details),nameof(Fit)}) Changed(name); }
    private void Changed(string name) => PropertyChanged?.Invoke(this,new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class MediaGroup(string key) : ObservableCollection<MediaCard> { public string Key { get; } = key; }
public sealed record SourceChoice(string? Id, string Label) { public override string ToString() => Label; }
