using System;
using EffectViewer.Rendering;

namespace EffectViewer.Runtime.Showcase
{
    internal interface IShowcaseScriptCallbacks : IDisposable
    {
        void Update(double deltaSeconds);

        bool TryDraw(FrameCaptureGraphics graphics);
    }
}
