using System.Numerics;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Controls
{
    public interface IEffectViewport
    {
        RenderFrame Frame { get; set; }
        ITextureSource TextureSource { get; set; }
        IRenderFrameProvider FrameProvider { get; set; }
        ViewportBackgroundMode BackgroundMode { get; set; }

        void SetViewTransform(float zoom, Vector2 panPixels);
    }
}
