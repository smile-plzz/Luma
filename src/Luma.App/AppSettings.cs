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
    public bool CommonOnly { get; set; }
    public int FilterDefaultsVersion { get; set; } = 1;
    public bool IncludeDescendants { get; set; } = true;
    public int CacheGiB { get; set; } = 2;
    public string Theme { get; set; } = "Default";
    public static bool IsSmokeTest => Environment.GetCommandLineArgs().Contains("--smoke-test");
    private static string SmokeSession
    {
        get
        {
            var value=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--smoke-session="))?["--smoke-session=".Length..];
            return Guid.TryParse(value,out var id) ? id.ToString("N") : Environment.ProcessId.ToString();
        }
    }
    public static string LocalRoot => IsSmokeTest ? Path.Combine(Path.GetTempPath(), "Luma-smoke-" + SmokeSession)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luma");
    public static AppSettings Load()
    {
        try { return FromJson(File.ReadAllText(Path.Combine(LocalRoot,"settings.json"))); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public static AppSettings FromJson(string json)
    {
        var settings=JsonSerializer.Deserialize<AppSettings>(json) ?? new();
        using var document=JsonDocument.Parse(json);
        // Older versions enabled this filter by default. Reveal supported files once
        // on upgrade; subsequent explicit choices remain persistent.
        if(document.RootElement.ValueKind==JsonValueKind.Object && !document.RootElement.TryGetProperty(nameof(FilterDefaultsVersion),out _))
            settings.CommonOnly=false;
        settings.FilterDefaultsVersion=1;
        return settings;
    }
    public void Save()
    {
        Directory.CreateDirectory(LocalRoot);
        var path = Path.Combine(LocalRoot,"settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this)); File.Move(path + ".tmp", path, true);
    }
}
