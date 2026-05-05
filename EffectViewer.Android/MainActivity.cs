using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia;
using Avalonia.Android;

namespace EffectViewer.Android
{
    [Activity(
        Label = "Effect Viewer",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.Orientation |
                               ConfigChanges.ScreenSize |
                               ConfigChanges.UiMode |
                               ConfigChanges.Keyboard |
                               ConfigChanges.KeyboardHidden)]
    public class MainActivity : AvaloniaMainActivity
    {
    }
}
