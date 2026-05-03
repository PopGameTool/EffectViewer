using Avalonia;
using EffectViewer.Controls;
using System;

namespace EffectViewer.Desktop
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
            App.ProjectStorageProvider = new DesktopProjectStorageProvider();
            OpenGlEffectViewport.GlInterfaceFactory = new DesktopGlInterfaceFactory();

            AppBuilder builder = AppBuilder.Configure<App>()
                .UsePlatformDetect();

            if (OperatingSystem.IsMacOS())
            {
                builder = builder.With(new AvaloniaNativePlatformOptions
                {
                    RenderingMode = new[] { AvaloniaNativeRenderingMode.OpenGl }
                });
            }

#if DEBUG
            builder = builder.WithDeveloperTools();
#endif
            return builder
                .WithInterFont()
                .LogToTrace();
        }
    }
}
