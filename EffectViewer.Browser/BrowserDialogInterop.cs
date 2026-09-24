using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace EffectViewer.Browser;

[SupportedOSPlatform("browser")]
internal static partial class BrowserDialogInterop
{
    private const string ModuleName = "effectviewer.dialog";
    private static Task<JSObject>? _moduleTask;

    public static Task<JSObject> EnsureLoadedAsync() =>
        _moduleTask ??= JSHost.ImportAsync(ModuleName, "/effectDialog.js");

    [JSImport("setDialogOverlayActive", ModuleName)]
    public static partial void SetDialogOverlayActive(bool active);
}
