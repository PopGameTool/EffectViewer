using System.Numerics;

namespace EffectViewer.Rendering
{
    public interface IViewportDragHandler
    {
        bool CanDrag { get; }
        bool IsPanModeEnabled { get; }
        ViewportTransformBox? TransformBox { get; }
        void BeginDrag(ViewportDragHandle handle, Vector2 worldPosition);
        void DragTo(Vector2 worldPosition);
        void EndDrag();
    }
}
