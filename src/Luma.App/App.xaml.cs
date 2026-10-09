using Microsoft.UI.Xaml;

namespace Luma.App;
public partial class App : Application
{
    private Window? window;
    public App()
    {
        UnhandledException += (_, e) => Log(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
        try { InitializeComponent(); } catch (Exception ex) { Log(ex); throw; }
    }
    private static void Log(Exception? exception)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.txt"), DateTime.UtcNow + " " + exception + Environment.NewLine); } catch { }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { window = new MainWindow(); window.Activate(); } catch (Exception ex) { Log(ex); throw; }
    }
}
