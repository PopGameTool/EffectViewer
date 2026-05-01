using Avalonia;
using Avalonia.Browser;
using EffectViewer;
using System.Threading.Tasks;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
    {
        App.ProjectStorageProvider = new EffectViewer.Browser.BrowserProjectStorageProvider();
        return AppBuilder.Configure<App>();
    }
}
