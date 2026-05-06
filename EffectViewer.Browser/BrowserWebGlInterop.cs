using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace EffectViewer.Browser
{
    [SupportedOSPlatform("browser")]
    internal static partial class BrowserWebGlInterop
    {
        private const string ModuleName = "effectviewer.webgl.viewport";
        private static Task<JSObject>? _moduleTask;

        public static Task<JSObject> EnsureLoadedAsync()
        {
            return _moduleTask ??= JSHost.ImportAsync(ModuleName, "/effectWebGlViewport.js");
        }

        [JSImport("createViewport", ModuleName)]
        public static partial JSObject CreateViewport();

        [JSImport("destroyViewport", ModuleName)]
        public static partial void DestroyViewport(JSObject canvas);

        [JSImport("clearTextures", ModuleName)]
        public static partial void ClearTextures(JSObject canvas);

        [JSImport("getMaxTextureSize", ModuleName)]
        public static partial int GetMaxTextureSize(JSObject canvas);

        [JSImport("setDialogOverlayActive", ModuleName)]
        public static partial void SetDialogOverlayActive(bool active);

        [JSImport("uploadTexture", ModuleName)]
        public static partial bool UploadTexture(
            JSObject canvas,
            string id,
            int width,
            int height,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> rgbaPixels,
            int byteCount);

        [JSImport("renderFrame", ModuleName)]
        public static partial void RenderFrame(
            JSObject canvas,
            int width,
            int height,
            float clearRed,
            float clearGreen,
            float clearBlue,
            float clearAlpha,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> vertexBytes,
            int vertexByteCount,
            int[] batchFirstVertices,
            int[] batchVertexCounts,
            int[] batchBlendModes,
            string[] batchTextureIds);

        [JSImport("readPixels", ModuleName)]
        public static partial bool ReadPixels(
            JSObject canvas,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> rgbaPixels,
            int byteCount);
    }
}
