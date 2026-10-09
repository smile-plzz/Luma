using System.ComponentModel;
using Luma.Core;
using Microsoft.UI.Xaml.Media.Imaging;
namespace Luma.App;
public sealed class MediaCard(LibraryItem item) : INotifyPropertyChanged
{
    public LibraryItem Item { get; } = item;
    public string Name => Path.GetFileName(Item.Media.RelativePath);
    public string Month => new DateTime(Item.Media.ModifiedTicks, DateTimeKind.Utc).ToLocalTime().ToString("MMMM yyyy");
    public string Caption => $"{(Item.Media.Kind == "video" ? "VIDEO" : "PHOTO")}  ·  {new DateTime(Item.Media.ModifiedTicks, DateTimeKind.Utc).ToLocalTime():dd MMM yyyy}";
    public string State => (Item.Favorite ? "★  " : "") + (Item.Media.IsAvailable ? "" : "Offline");
    private BitmapImage? thumbnail;
    public BitmapImage? Thumbnail { get => thumbnail; set { thumbnail = value; PropertyChanged?.Invoke(this,new(nameof(Thumbnail))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed record SourceChoice(string? Id, string Label) { public override string ToString() => Label; }
