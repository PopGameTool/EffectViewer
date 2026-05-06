using Avalonia;
using Avalonia.Browser;
using EffectViewer;
using EffectViewer.Controls;
using EffectViewer.Views;
using System.Threading.Tasks;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        await EffectViewer.Browser.BrowserWebGlInterop.EnsureLoadedAsync();
        MainView.BrowserDialogOverlayActiveChanged = EffectViewer.Browser.BrowserWebGlInterop.SetDialogOverlayActive;

        await BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartBrowserAppAsync(
                "out",
                new BrowserPlatformOptions
                {
                    PreferFileDialogPolyfill = true
                });
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        App.ProjectStorageProvider = new EffectViewer.Browser.BrowserProjectStorageProvider();
        InteractiveEffectViewport.ViewportFactory = static () => new EffectViewer.Browser.BrowserAvaloniaWebGlEffectViewport();
        return AppBuilder.Configure<App>();
    }
}
