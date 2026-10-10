using System.Text.Json;
namespace Luma.App;
public sealed class AppSettings
{
    public int SortIndex { get; set; } = 4;
    public bool Descending { get; set; } = true;
    public int GroupIndex { get; set; } = 2;
    public int ThumbnailSize { get; set; } = 190;
    public bool ShowNames { get; set; }
    public bool ShowDetails { get; set; }
    public bool FitImages { get; set; }
    public bool Compact { get; set; }
    public bool CommonOnly { get; set; } = true;
    public bool IncludeDescendants { get; set; } = true;
    public int CacheGiB { get; set; } = 2;
    public string Theme { get; set; } = "Default";
    public static bool IsSmokeTest => Environment.GetCommandLineArgs().Contains("--smoke-test");
    public static string LocalRoot => IsSmokeTest ? Path.Combine(Path.GetTempPath(), "Luma-smoke-" + Environment.ProcessId)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luma");
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
