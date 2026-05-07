using Avalonia;
using EffectViewer.Controls;
using System;

namespace EffectViewer.Windows
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            App.ProjectStorageProvider = new WindowsProjectStorageProvider();
            OpenGlEffectViewport.GlInterfaceFactory = new WindowsGlInterfaceFactory();

            AppBuilder builder = AppBuilder.Configure<App>()
                .UsePlatformDetect();

#if DEBUG
            builder = builder.WithDeveloperTools();
#endif
            return builder
                .WithInterFont()
                .LogToTrace();
        }
    }
}
