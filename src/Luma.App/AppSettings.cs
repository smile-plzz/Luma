using System.Text.Json;
namespace Luma.App;
public sealed class AppSettings
{
    public int CacheGiB { get; set; } = 2;
    public string Theme { get; set; } = "Default";
    public static string LocalRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luma");
    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(LocalRoot,"settings.json"))) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(LocalRoot);
        var path = Path.Combine(LocalRoot,"settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this)); File.Move(path + ".tmp", path, true);
    }
}
