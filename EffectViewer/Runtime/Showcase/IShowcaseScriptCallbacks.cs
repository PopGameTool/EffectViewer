using System;
using EffectViewer.Rendering;

namespace EffectViewer.Runtime.Showcase
{
    internal interface IShowcaseScriptCallbacks : IDisposable
    {
        bool HasUpdate { get; }

        bool HasDraw { get; }

        void Update(double deltaSeconds);

        bool TryDraw(FrameCaptureGraphics graphics);
    }
}
