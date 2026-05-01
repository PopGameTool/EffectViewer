using Avalonia;
using Avalonia.Browser;
using EffectViewer;
using EffectViewer.Controls;
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
        OpenGlEffectViewport.GlInterfaceFactory = new EffectViewer.Browser.WebGlInterfaceFactory();
        return AppBuilder.Configure<App>();
    }
}
