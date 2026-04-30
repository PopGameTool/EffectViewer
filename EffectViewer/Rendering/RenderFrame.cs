using System.Collections.ObjectModel;
using System.Numerics;

namespace EffectViewer.Rendering
{
    public sealed class RenderFrame
    {
        public Vector4 ClearColor { get; set; } = new(0.08f, 0.09f, 0.1f, 1f);
        public ObservableCollection<RenderSpriteCommand> Sprites { get; } = [];
        public ObservableCollection<RenderMeshCommand> Meshes { get; } = [];

        public void ClearCommands()
        {
            Sprites.Clear();
            Meshes.Clear();
        }
    }
}
