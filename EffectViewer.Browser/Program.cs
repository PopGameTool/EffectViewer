using Avalonia;
using Avalonia.Browser;
using EffectViewer;
using EffectViewer.Controls;
using System.Threading.Tasks;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        await EffectViewer.Browser.BrowserWebGlInterop.EnsureLoadedAsync();

        await BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        App.ProjectStorageProvider = new EffectViewer.Browser.BrowserProjectStorageProvider();
        InteractiveEffectViewport.ViewportFactory = static () => new EffectViewer.Browser.BrowserWebGlEffectViewport();
        return AppBuilder.Configure<App>();
    }
}
