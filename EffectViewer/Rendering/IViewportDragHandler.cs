using System.Numerics;

namespace EffectViewer.Rendering
{
    public interface IViewportDragHandler
    {
        bool CanDrag { get; }
        void DragBy(Vector2 worldDelta);
    }
}
