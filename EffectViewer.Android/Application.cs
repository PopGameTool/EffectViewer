using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using EffectViewer.Controls;

namespace EffectViewer.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.ProjectStorageProvider = new AndroidProjectStorageProvider(this);
            OpenGlEffectViewport.GlInterfaceFactory = new AndroidGlInterfaceFactory();
            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
