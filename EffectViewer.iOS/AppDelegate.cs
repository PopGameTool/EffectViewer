using Avalonia;
using Avalonia.iOS;
using EffectViewer.Controls;
using Foundation;
using UIKit;

namespace EffectViewer.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.ProjectStorageProvider = new IosProjectStorageProvider();
            OpenGlEffectViewport.GlInterfaceFactory = new IosGlInterfaceFactory();
            return base.CustomizeAppBuilder(builder)
                .With(new iOSPlatformOptions
                {
                    RenderingMode = [iOSRenderingMode.OpenGl]
                })
                .WithInterFont();
        }
    }
}
